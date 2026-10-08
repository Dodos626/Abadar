using Abadar.Backend.Data;
using Abadar.Backend.Models;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace Abadar.Backend.Services;

// Publishes durable outbox events to Kafka with at-least-once delivery.
public sealed class OutboxPublisher(
    IServiceProvider services,
    KafkaOptions options,
    KafkaRuntimeState runtimeState,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    // Polls unpublished events and marks each row only after Kafka acknowledges it.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            return;
        }

        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = int.MaxValue
        }).Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishBatchAsync(producer, stoppingToken);
                runtimeState.MarkPublisherConnected();
                await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                runtimeState.MarkFailure(exception.Message);
                logger.LogError(exception, "Outbox publication failed; durable events remain pending");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        producer.Flush(TimeSpan.FromSeconds(10));
    }

    // Publishes one bounded database batch while preserving unpublished failures.
    private async Task PublishBatchAsync(
        IProducer<string, string> producer,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AbadarDbContext>();
        var events = await dbContext.OutboxEvents
            .Where(value => value.PublishedAt == null)
            .OrderBy(value => value.OccurredAt)
            .ThenBy(value => value.Sequence)
            .Take(200)
            .ToListAsync(cancellationToken);

        foreach (var outboxEvent in events)
        {
            try
            {
                await producer.ProduceAsync(
                    outboxEvent.Topic,
                    new Message<string, string>
                    {
                        Key = outboxEvent.PartitionKey,
                        Value = outboxEvent.Payload,
                        Headers =
                        [
                            new Header("event_id", System.Text.Encoding.UTF8.GetBytes(outboxEvent.EventId.ToString())),
                            new Header("event_type", System.Text.Encoding.UTF8.GetBytes(outboxEvent.EventType))
                        ]
                    },
                    cancellationToken);
                outboxEvent.PublishedAt = DateTimeOffset.UtcNow;
                outboxEvent.Attempts++;
                outboxEvent.LastError = null;
            }
            catch (Exception exception)
            {
                outboxEvent.Attempts++;
                outboxEvent.LastError = exception.Message;
                await dbContext.SaveChangesAsync(cancellationToken);
                throw;
            }
        }

        if (events.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}