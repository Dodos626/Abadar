using Xunit;

namespace Abadar.MatchingEngine.Tests;

// Verifies the channel-based symbol ownership model under concurrent calls.
public sealed class MatchingEngineConcurrencyTests
{
    [Fact]
    // Verifies concurrent submissions receive unique sequences without lost orders.
    public async Task ConcurrentOrders_AreSequencedWithoutLoss()
    {
        await using var engine = new MatchingEngine();
        const int count = 1_000;

        var submissions = Enumerable.Range(0, count)
            .Select(_ => engine.SubmitAsync(OrderBookTests.Limit(OrderSide.Buy, 1, 100)).AsTask());
        var results = await Task.WhenAll(submissions);
        var snapshot = await engine.SnapshotAsync("BTC/USD");

        Assert.Equal(count, results.Select(result => result.Sequence).Distinct().Count());
        Assert.Equal(count, snapshot.Bids.Single().Orders.Count);
        Assert.Equal(count, snapshot.Bids.Single().Quantity);
    }

    [Fact]
    // Verifies concurrent crossing orders preserve total executed quantity.
    public async Task ConcurrentCrossingOrders_PreserveQuantity()
    {
        await using var engine = new MatchingEngine();
        const int count = 500;

        var buys = Enumerable.Range(0, count)
            .Select(_ => engine.SubmitAsync(OrderBookTests.Limit(OrderSide.Buy, 1, 100)).AsTask());
        await Task.WhenAll(buys);

        var sells = Enumerable.Range(0, count)
            .Select(_ => engine.SubmitAsync(OrderBookTests.Limit(OrderSide.Sell, 1, 100)).AsTask());
        var results = await Task.WhenAll(sells);
        var snapshot = await engine.SnapshotAsync("BTC/USD");

        Assert.Equal(count, results.Sum(result => result.Executions.Sum(trade => trade.Quantity)));
        Assert.Empty(snapshot.Bids);
        Assert.Empty(snapshot.Asks);
    }

    [Fact]
    // Verifies different symbols maintain independent order books.
    public async Task DifferentSymbols_HaveIndependentBooks()
    {
        await using var engine = new MatchingEngine();
        var btc = OrderBookTests.Limit(OrderSide.Buy, 1, 100);
        var eth = btc with { Id = Guid.NewGuid(), Symbol = "ETH/USD", Price = 200 };

        await Task.WhenAll(
            engine.SubmitAsync(btc).AsTask(),
            engine.SubmitAsync(eth).AsTask());

        Assert.Equal(100, (await engine.SnapshotAsync("BTC/USD")).Bids.Single().Price);
        Assert.Equal(200, (await engine.SnapshotAsync("ETH/USD")).Bids.Single().Price);
    }

    [Fact]
    // Verifies recovered FIFO state and sequence counters continue deterministically.
    public async Task Recovery_RestoresBookAndContinuesSequences()
    {
        await using var engine = new MatchingEngine();
        var first = OrderBookTests.Limit(OrderSide.Buy, 5, 100);
        var second = OrderBookTests.Limit(OrderSide.Buy, 3, 100);

        await engine.RecoverAsync("BTC/USD",
        [
            new RecoveredOrder(first, 8, 2, OrderStatus.PartiallyFilled),
            new RecoveredOrder(second, 9, 3, OrderStatus.Open)
        ], 9, 12);

        var result = await engine.SubmitAsync(OrderBookTests.Limit(OrderSide.Sell, 4, 100));

        Assert.Equal(10, result.Sequence);
        Assert.Equal([13L, 14L], result.Executions.Select(value => value.Sequence));
        Assert.Equal([first.Id, second.Id], result.Executions.Select(value => value.BuyOrderId));
        Assert.Equal(1, (await engine.SnapshotAsync("BTC/USD")).Bids.Single().Quantity);

        await engine.ResetAsync();
        Assert.Empty((await engine.SnapshotAsync("BTC/USD")).Bids);
    }
}