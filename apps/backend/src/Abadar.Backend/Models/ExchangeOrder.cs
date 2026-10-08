namespace Abadar.Backend.Models;

// Persists the complete lifecycle state of one submitted exchange order.
public sealed class ExchangeOrder
{
    // Stores the matching-engine order identifier.
    public Guid Id { get; set; }
    // Identifies the simulated or user account that submitted the order.
    public Guid AccountId { get; set; }
    // Stores the normalized trading pair handled by the order book.
    public required string Symbol { get; set; }
    // Stores the normalized buy or sell direction.
    public required string Side { get; set; }
    // Stores whether the order is limit or market.
    public required string Type { get; set; }
    // Stores the limit price or null for market orders.
    public decimal? Price { get; set; }
    // Stores the originally submitted quantity.
    public decimal Quantity { get; set; }
    // Tracks quantity that has not yet executed.
    public decimal RemainingQuantity { get; set; }
    // Stores the durable order lifecycle status.
    public required string Status { get; set; }
    // Preserves deterministic order arrival within a symbol.
    public long Sequence { get; set; }
    // Records when the order was first persisted.
    public DateTimeOffset CreatedAt { get; set; }
    // Records the latest durable lifecycle change.
    public DateTimeOffset UpdatedAt { get; set; }
}