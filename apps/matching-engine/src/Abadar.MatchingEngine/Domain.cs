namespace Abadar.MatchingEngine;

// Identifies an exchange asset with a normalized symbol.
public sealed record Asset(string Symbol)
{
    // Stores the validated uppercase asset symbol.
    public string Symbol { get; } = Require(Symbol, nameof(Symbol));

    // Validates and normalizes a required symbol value.
    private static string Require(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A symbol is required.", name)
            : value.Trim().ToUpperInvariant();
}

// Combines base and quote assets into a trading symbol.
public sealed record TradingPair(Asset Base, Asset Quote)
{
    // Formats the pair as BASE/QUOTE.
    public string Symbol => $"{Base.Symbol}/{Quote.Symbol}";
}

// Defines whether an order buys or sells the base asset.
public enum OrderSide
{
    Buy,
    Sell
}

// Defines whether an order has a limit price or crosses the market.
public enum OrderType
{
    Limit,
    Market
}

// Describes the current lifecycle state of an order.
public enum OrderStatus
{
    Open,
    PartiallyFilled,
    Filled,
    Cancelled
}

// Carries an order submission into the pure matching engine.
public sealed record OrderRequest(
    Guid Id,
    Guid AccountId,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Quantity,
    decimal? Price = null);

// Records one completed match between a buy order and a sell order.
public sealed record Execution(
    Guid BuyOrderId,
    Guid SellOrderId,
    string Symbol,
    decimal Price,
    decimal Quantity,
    long Sequence);

// Returns the resulting state and executions for a submitted order.
public sealed record OrderResult(
    Guid OrderId,
    long Sequence,
    OrderStatus Status,
    decimal RemainingQuantity,
    IReadOnlyList<Execution> Executions);

// Exposes immutable details about an order currently resting on the book.
public sealed record RestingOrder(
    Guid Id,
    long Sequence,
    decimal Price,
    decimal RemainingQuantity);

// Groups resting orders and total quantity at one price.
public sealed record PriceLevel(
    decimal Price,
    decimal Quantity,
    IReadOnlyList<RestingOrder> Orders);

// Captures the current bid and ask state for one symbol.
public sealed record OrderBookSnapshot(
    string Symbol,
    IReadOnlyList<PriceLevel> Bids,
    IReadOnlyList<PriceLevel> Asks);