using Xunit;

namespace Abadar.MatchingEngine.Tests;

// Verifies the order book's matching and lifecycle rules.
public sealed class OrderBookTests
{
    [Fact]
    // Verifies that an unmatched limit order rests on the correct side.
    public void LimitOrder_RestsOnBook()
    {
        var book = new OrderBook("BTC/USD");
        var order = Limit(OrderSide.Buy, 2, 100);

        var result = book.Submit(order);
        var snapshot = book.Snapshot();

        Assert.Equal(OrderStatus.Open, result.Status);
        Assert.Equal(100, snapshot.Bids.Single().Price);
        Assert.Equal(2, snapshot.Bids.Single().Quantity);
        Assert.Empty(snapshot.Asks);
    }

    [Fact]
    // Verifies crossing limits execute at the older resting order's price.
    public void CrossingLimits_TradeAtRestingPrice()
    {
        var book = new OrderBook("BTC/USD");
        var sell = Limit(OrderSide.Sell, 2, 100);
        book.Submit(sell);

        var result = book.Submit(Limit(OrderSide.Buy, 2, 105));

        var trade = Assert.Single(result.Executions);
        Assert.Equal(100, trade.Price);
        Assert.Equal(2, trade.Quantity);
        Assert.Equal(OrderStatus.Filled, result.Status);
        Assert.Empty(book.Snapshot().Asks);
    }

    [Fact]
    // Verifies a smaller incoming order leaves the correct resting quantity.
    public void PartialFill_LeavesRemainingQuantity()
    {
        var book = new OrderBook("BTC/USD");
        var buy = Limit(OrderSide.Buy, 10, 100);
        book.Submit(buy);

        var result = book.Submit(Limit(OrderSide.Sell, 4, 100));
        var remaining = Assert.Single(book.Snapshot().Bids).Orders.Single();

        Assert.Equal(OrderStatus.Filled, result.Status);
        Assert.Equal(4, Assert.Single(result.Executions).Quantity);
        Assert.Equal(6, remaining.RemainingQuantity);
    }

    [Fact]
    // Verifies equal-price orders execute in first-in-first-out order.
    public void SamePrice_UsesFifo()
    {
        var book = new OrderBook("BTC/USD");
        var first = Limit(OrderSide.Buy, 10, 100);
        var second = Limit(OrderSide.Buy, 5, 100);
        book.Submit(first);
        book.Submit(second);

        var result = book.Submit(Limit(OrderSide.Sell, 12, 100));

        Assert.Equal([first.Id, second.Id], result.Executions.Select(x => x.BuyOrderId));
        var remaining = Assert.Single(book.Snapshot().Bids).Orders.Single();
        Assert.Equal(second.Id, remaining.Id);
        Assert.Equal(3, remaining.RemainingQuantity);
    }

    [Fact]
    // Verifies an incoming order consumes the best available price first.
    public void IncomingOrder_MatchesBestPriceFirst()
    {
        var book = new OrderBook("BTC/USD");
        book.Submit(Limit(OrderSide.Sell, 1, 101));
        book.Submit(Limit(OrderSide.Sell, 1, 100));

        var result = book.Submit(Limit(OrderSide.Buy, 2, 101));

        Assert.Equal([100m, 101m], result.Executions.Select(x => x.Price));
    }

    [Fact]
    // Verifies a limit order never executes beyond its specified price.
    public void Limits_DoNotTradeOutsideTheirPrice()
    {
        var book = new OrderBook("BTC/USD");
        book.Submit(Limit(OrderSide.Sell, 1, 101));

        var result = book.Submit(Limit(OrderSide.Buy, 1, 100));

        Assert.Empty(result.Executions);
        Assert.Equal(100, book.Snapshot().Bids.Single().Price);
        Assert.Equal(101, book.Snapshot().Asks.Single().Price);
    }

    [Fact]
    // Verifies a market order crosses levels and never rests on the book.
    public void MarketOrder_CrossesLevelsAndDoesNotRest()
    {
        var book = new OrderBook("BTC/USD");
        book.Submit(Limit(OrderSide.Sell, 1, 100));
        book.Submit(Limit(OrderSide.Sell, 1, 101));

        var result = book.Submit(Market(OrderSide.Buy, 3));

        Assert.Equal(2, result.Executions.Count);
        Assert.Equal(1, result.RemainingQuantity);
        Assert.Equal(OrderStatus.Cancelled, result.Status);
        Assert.Empty(book.Snapshot().Bids);
    }

    [Fact]
    // Verifies cancellation removes an order and repeated cancellation is safe.
    public void Cancel_RemovesOrderAndIsIdempotent()
    {
        var book = new OrderBook("BTC/USD");
        var order = Limit(OrderSide.Buy, 2, 100);
        book.Submit(order);

        Assert.True(book.Cancel(order.Id));
        Assert.False(book.Cancel(order.Id));
        Assert.Empty(book.Snapshot().Bids);
    }

    [Fact]
    // Verifies a cancelled order cannot participate in a later execution.
    public void CancelledOrder_CannotExecute()
    {
        var book = new OrderBook("BTC/USD");
        var buy = Limit(OrderSide.Buy, 2, 100);
        book.Submit(buy);
        book.Cancel(buy.Id);

        var sell = book.Submit(Limit(OrderSide.Sell, 2, 100));

        Assert.Empty(sell.Executions);
        Assert.Empty(book.Snapshot().Bids);
        Assert.Equal(2, book.Snapshot().Asks.Single().Quantity);
    }

    [Fact]
    // Verifies an order ID cannot be reused after its first submission.
    public void OrderId_CannotBeSubmittedTwice()
    {
        var book = new OrderBook("BTC/USD");
        var order = Limit(OrderSide.Buy, 1, 100);
        book.Submit(order);
        book.Cancel(order.Id);

        Assert.Throws<InvalidOperationException>(() => book.Submit(order));
    }

    [Fact]
    // Verifies execution sequence numbers are unique and increasing.
    public void ExecutionSequences_AreUniqueAndIncreasing()
    {
        var book = new OrderBook("BTC/USD");
        book.Submit(Limit(OrderSide.Sell, 1, 100));
        book.Submit(Limit(OrderSide.Sell, 1, 101));

        var result = book.Submit(Limit(OrderSide.Buy, 2, 101));

        Assert.Equal([1L, 2L], result.Executions.Select(x => x.Sequence));
    }

    [Fact]
    // Verifies invalid quantity, price, and symbol values are rejected.
    public void InvalidOrders_AreRejected()
    {
        var book = new OrderBook("BTC/USD");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            book.Submit(Limit(OrderSide.Buy, 0, 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            book.Submit(Limit(OrderSide.Buy, 1, 0)));
        Assert.Throws<ArgumentException>(() =>
            book.Submit(Limit(OrderSide.Buy, 1, 100) with { Symbol = "ETH/USD" }));
    }

    [Fact]
    // Verifies snapshots order bids high-to-low and asks low-to-high.
    public void Snapshot_OrdersBidsDescendingAndAsksAscending()
    {
        var book = new OrderBook("BTC/USD");
        book.Submit(Limit(OrderSide.Buy, 1, 99));
        book.Submit(Limit(OrderSide.Buy, 1, 100));
        book.Submit(Limit(OrderSide.Sell, 1, 102));
        book.Submit(Limit(OrderSide.Sell, 1, 101));

        var snapshot = book.Snapshot();

        Assert.Equal([100m, 99m], snapshot.Bids.Select(x => x.Price));
        Assert.Equal([101m, 102m], snapshot.Asks.Select(x => x.Price));
    }

    // Creates a valid BTC/USD limit-order request for a test.
    internal static OrderRequest Limit(OrderSide side, decimal quantity, decimal price) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "BTC/USD", side, OrderType.Limit, quantity, price);

    // Creates a valid BTC/USD market-order request for a test.
    internal static OrderRequest Market(OrderSide side, decimal quantity) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "BTC/USD", side, OrderType.Market, quantity);
}