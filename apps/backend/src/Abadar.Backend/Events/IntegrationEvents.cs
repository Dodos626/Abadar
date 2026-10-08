using System.Text.Json;
using Abadar.Backend.Models;

namespace Abadar.Backend.Events;

// Names the Version 2 topics and versioned event contracts.
public static class EventCatalog
{
    public const string OrdersTopic = "abadar.orders";
    public const string TradesTopic = "abadar.trades";
    public const string OrderAcceptedV1 = "OrderAccepted.v1";
    public const string TradeExecutedV1 = "TradeExecuted.v1";
}

// Wraps every Kafka payload with common identity, ordering, and version metadata.
public sealed record EventEnvelope<T>(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    string AggregateId,
    string AggregateType,
    long Sequence,
    int Version,
    T Payload);

// Describes an accepted order after matching has determined its durable status.
public sealed record OrderAcceptedEvent(
    Guid OrderId,
    Guid AccountId,
    string Symbol,
    string Side,
    string Type,
    decimal? Price,
    decimal Quantity,
    decimal RemainingQuantity,
    string Status);

// Describes one immutable execution consumed by Version 2 projections.
public sealed record TradeExecutedEvent(
    Guid TradeId,
    string Symbol,
    Guid BuyOrderId,
    Guid SellOrderId,
    Guid BuyerAccountId,
    Guid SellerAccountId,
    decimal Price,
    decimal Quantity);

// Creates durable outbox records from strongly typed event envelopes.
public static class IntegrationEventFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // Serializes a typed event and records its Kafka routing metadata.
    public static IntegrationEventOutbox Create<T>(
        EventEnvelope<T> envelope,
        string topic,
        string partitionKey) => new()
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            AggregateId = envelope.AggregateId,
            AggregateType = envelope.AggregateType,
            Sequence = envelope.Sequence,
            Version = envelope.Version,
            PartitionKey = partitionKey,
            Topic = topic,
            Payload = JsonSerializer.Serialize(envelope, JsonOptions),
            OccurredAt = envelope.OccurredAt
        };
}