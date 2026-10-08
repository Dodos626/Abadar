namespace Abadar.Backend.Models;

// Persists one immutable execution produced by the matching engine.
public sealed class ExchangeTrade
{
    // Identifies the durable trade record.
    public Guid Id { get; set; } = Guid.NewGuid();
    // Stores the normalized trading pair that executed.
    public required string Symbol { get; set; }
    // References the executed buy order.
    public Guid BuyOrderId { get; set; }
    // References the executed sell order.
    public Guid SellOrderId { get; set; }
    // Stores the resting-order execution price.
    public decimal Price { get; set; }
    // Stores the quantity exchanged by this execution.
    public decimal Quantity { get; set; }
    // Preserves deterministic execution order within a symbol.
    public long Sequence { get; set; }
    // Records when the execution became durable.
    public DateTimeOffset ExecutedAt { get; set; }
}