namespace Abadar.Backend.Models;

// Stores one integration event until Kafka acknowledges its publication.
public sealed class IntegrationEventOutbox
{
    // Identifies the event globally for consumer idempotency.
    public Guid EventId { get; set; }
    // Stores the versioned event contract name.
    public required string EventType { get; set; }
    // Stores the aggregate identifier represented by the event.
    public required string AggregateId { get; set; }
    // Stores the aggregate category represented by the event.
    public required string AggregateType { get; set; }
    // Preserves deterministic event ordering within the symbol stream.
    public long Sequence { get; set; }
    // Stores the event schema version.
    public int Version { get; set; }
    // Stores the Kafka partition key, currently the trading symbol.
    public required string PartitionKey { get; set; }
    // Stores the Kafka topic selected for publication.
    public required string Topic { get; set; }
    // Stores the serialized event envelope.
    public required string Payload { get; set; }
    // Records when the business transaction created the event.
    public DateTimeOffset OccurredAt { get; set; }
    // Records when Kafka acknowledged the event.
    public DateTimeOffset? PublishedAt { get; set; }
    // Tracks publication attempts for diagnostics.
    public int Attempts { get; set; }
    // Stores the latest publication error without losing the event.
    public string? LastError { get; set; }
}