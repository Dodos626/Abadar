using Abadar.Backend.Data;
using Abadar.Backend.Events;
using Abadar.Backend.Models;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace Abadar.Backend.Services;

public interface IEventReplayService
{
    Task<EventReplayResponse> ReplayAsync(string consumer, CancellationToken cancellationToken);
    Task<EventSystemStatusResponse> GetStatusAsync(CancellationToken cancellationToken);
}

// Resets one projection and gives its Kafka group a fresh replay identity.
public sealed class EventReplayService(
    AbadarDbContext dbContext,
    KafkaOptions options,
    KafkaRuntimeState runtimeState,
    TradeProjectionProcessor projectionProcessor) : IEventReplayService
{
    private static readonly string[] Consumers = ["portfolio", "market", "analytics"];

    // Clears a selected projection and its idempotency markers for replay.
    public async Task<EventReplayResponse> ReplayAsync(
        string consumer,
        CancellationToken cancellationToken)
    {
        var normalized = consumer.Trim().ToLowerInvariant();
        if (!Consumers.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ApiValidationException(new Dictionary<string, string[]>
            {
                ["consumer"] = ["Consumer must be portfolio, market, or analytics."]
            });
        }

        if (normalized == "portfolio")
        {
            dbContext.PortfolioPositions.RemoveRange(dbContext.PortfolioPositions);
        }
        else if (normalized == "market")
        {
            dbContext.MarketProjections.RemoveRange(dbContext.MarketProjections);
        }
        else
        {
            dbContext.AnalyticsProjections.RemoveRange(dbContext.AnalyticsProjections);
        }

        var processed = await dbContext.ProcessedEvents
            .Where(value => value.Consumer == normalized)
            .ToListAsync(cancellationToken);
        dbContext.ProcessedEvents.RemoveRange(processed);
        await dbContext.SaveChangesAsync(cancellationToken);

        var replayedEvents = 0;
        if (options.Enabled)
        {
            replayedEvents = await ReplayTradeTopicAsync(normalized, cancellationToken);
        }

        return new EventReplayResponse(normalized, processed.Count, replayedEvents, DateTimeOffset.UtcNow);
    }

    // Reports durable outbox and projection progress for the administrator UI.
    public async Task<EventSystemStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var outboxPending = await dbContext.OutboxEvents.CountAsync(
            value => value.PublishedAt == null,
            cancellationToken);
        var outboxPublished = await dbContext.OutboxEvents.CountAsync(
            value => value.PublishedAt != null,
            cancellationToken);
        var consumerProgress = await dbContext.ProcessedEvents
            .GroupBy(value => value.Consumer)
            .Select(group => new ConsumerProgressResponse(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        return new EventSystemStatusResponse(
            options.Enabled,
            runtimeState.TopicsReady,
            runtimeState.PublisherConnected,
            runtimeState.LastError,
            outboxPending,
            outboxPublished,
            consumerProgress);
    }

    // Reads the retained trade topic from the beginning into one selected projection.
    private async Task<int> ReplayTradeTopicAsync(
        string consumer,
        CancellationToken cancellationToken)
    {
        using var kafkaConsumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = $"abadar-replay-{consumer}-{Guid.NewGuid():N}",
            EnableAutoCommit = false,
            EnablePartitionEof = true,
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();
        kafkaConsumer.Assign(Enumerable.Range(0, options.TopicPartitions)
            .Select(partition => new TopicPartitionOffset(
                EventCatalog.TradesTopic,
                new Partition(partition),
                Offset.Beginning)));

        var completedPartitions = new HashSet<TopicPartition>();
        var processed = 0;
        while (completedPartitions.Count < options.TopicPartitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = kafkaConsumer.Consume(TimeSpan.FromMilliseconds(500));
            if (result is null)
            {
                continue;
            }
            if (result.IsPartitionEOF)
            {
                completedPartitions.Add(result.TopicPartition);
                continue;
            }

            var envelope = System.Text.Json.JsonSerializer.Deserialize<EventEnvelope<TradeExecutedEvent>>(
                result.Message.Value,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
                }) ?? throw new InvalidOperationException("Kafka replay payload was empty.");
            if (await projectionProcessor.ProcessAsync(consumer, envelope, cancellationToken))
            {
                processed++;
            }
        }

        return processed;
    }

}