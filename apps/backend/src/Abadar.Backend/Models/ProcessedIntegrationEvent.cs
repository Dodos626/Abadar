namespace Abadar.Backend.Models;

// Records one consumer/event pair so duplicate Kafka delivery is harmless.
public sealed class ProcessedIntegrationEvent
{
    // Identifies the logical consumer projection.
    public required string Consumer { get; set; }
    // Identifies the globally unique event already applied by that consumer.
    public Guid EventId { get; set; }
    // Records when the consumer transaction completed.
    public DateTimeOffset ProcessedAt { get; set; }
}