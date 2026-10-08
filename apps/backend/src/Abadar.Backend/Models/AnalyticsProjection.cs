namespace Abadar.Backend.Models;

// Stores an eventually consistent per-symbol analytics projection.
public sealed class AnalyticsProjection
{
    // Identifies the projected trading pair.
    public required string Symbol { get; set; }
    // Counts trade events applied by the analytics consumer.
    public long TradeCount { get; set; }
    // Accumulates executed quantity across all observed trades.
    public decimal TotalQuantity { get; set; }
    // Accumulates quote notional as price multiplied by quantity.
    public decimal TotalNotional { get; set; }
    // Records the latest execution time.
    public DateTimeOffset UpdatedAt { get; set; }
}