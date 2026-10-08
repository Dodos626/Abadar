using System.Text.Json;
using Abadar.Backend.Events;
using Confluent.Kafka;

namespace Abadar.Backend.Services;

// Provides the common Kafka loop and manual-commit boundary for trade projections.
public abstract class TradeEventConsumer(
    IServiceProvider services,
    KafkaOptions options,
    ILogger logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    protected abstract string ConsumerName { get; }

    // Consumes trades and commits Kafka offsets only after idempotent projection success.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            return;
        }

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = $"abadar-{ConsumerName}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();

        consumer.Subscribe(EventCatalog.TradesTopic);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(TimeSpan.FromMilliseconds(250));
                    if (result is null)
                    {
                        continue;
                    }
                    var envelope = JsonSerializer.Deserialize<EventEnvelope<TradeExecutedEvent>>(
                        result.Message.Value,
                        JsonOptions) ?? throw new InvalidOperationException("Kafka trade event payload was empty.");
                    if (string.Equals(envelope.EventType, EventCatalog.TradeExecutedV1, StringComparison.Ordinal))
                    {
                        await using var scope = services.CreateAsyncScope();
                        var processor = scope.ServiceProvider.GetRequiredService<TradeProjectionProcessor>();
                        await processor.ProcessAsync(ConsumerName, envelope, stoppingToken);
                    }
                    consumer.Commit(result);
                }
                catch (ConsumeException exception)
                {
                    logger.LogError(exception, "{Consumer} failed to consume a Kafka record", ConsumerName);
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal hosted-service shutdown.
        }
        finally
        {
            consumer.Close();
        }
    }
}

// Maintains account positions from the shared trade stream.
public sealed class PortfolioTradeConsumer(
    IServiceProvider services,
    KafkaOptions options,
    ILogger<PortfolioTradeConsumer> logger)
    : TradeEventConsumer(services, options, logger)
{
    protected override string ConsumerName => "portfolio";
}

// Maintains ticker state from the shared trade stream.
public sealed class MarketTradeConsumer(
    IServiceProvider services,
    KafkaOptions options,
    ILogger<MarketTradeConsumer> logger)
    : TradeEventConsumer(services, options, logger)
{
    protected override string ConsumerName => "market";
}

// Maintains aggregate statistics from the shared trade stream.
public sealed class AnalyticsTradeConsumer(
    IServiceProvider services,
    KafkaOptions options,
    ILogger<AnalyticsTradeConsumer> logger)
    : TradeEventConsumer(services, options, logger)
{
    protected override string ConsumerName => "analytics";
}