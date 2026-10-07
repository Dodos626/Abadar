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
}