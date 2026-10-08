namespace Abadar.Backend.Models;

// Stores an eventually consistent portfolio projection derived from trade events.
public sealed class PortfolioPosition
{
    // Identifies the simulated account whose position is projected.
    public Guid AccountId { get; set; }
    // Identifies the base asset held by the account.
    public required string Asset { get; set; }
    // Stores signed net base-asset quantity.
    public decimal Quantity { get; set; }
    // Records the latest applied event time.
    public DateTimeOffset UpdatedAt { get; set; }
}