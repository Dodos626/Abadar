namespace Abadar.Backend.Models;

// Stores an eventually consistent ticker projection derived from trade events.
public sealed class MarketProjection
{
    // Identifies the projected trading pair.
    public required string Symbol { get; set; }
    // Stores the latest execution price.
    public decimal LastPrice { get; set; }
    // Accumulates executed base-asset volume.
    public decimal Volume { get; set; }
    // Counts executions observed by the market consumer.
    public long TradeCount { get; set; }
    // Records the latest execution time.
    public DateTimeOffset UpdatedAt { get; set; }
}