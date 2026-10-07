using System.Diagnostics;
using Abadar.MatchingEngine;

// Reads the requested workload size and creates reusable benchmark state.
var orderCount = args.Length > 0 && int.TryParse(args[0], out var parsed) ? parsed : 100_000;
var accountId = Guid.NewGuid();
var book = new OrderBook("BTC/USD");

// Warm up JIT compilation before measuring.
for (var index = 0; index < 1_000; index++)
{
    book.Submit(Limit(OrderSide.Buy, accountId));
    book.Submit(Limit(OrderSide.Sell, accountId));
}

// Resets the book and allocates measurement storage after warm-up.
book = new OrderBook("BTC/USD");
var latencies = new long[orderCount];
var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
var stopwatch = Stopwatch.StartNew();
var trades = 0;

// Measures alternating buy and sell submissions at one crossing price.
for (var index = 0; index < orderCount; index++)
{
    var side = index % 2 == 0 ? OrderSide.Buy : OrderSide.Sell;
    var started = Stopwatch.GetTimestamp();
    trades += book.Submit(Limit(side, accountId)).Executions.Count;
    latencies[index] = Stopwatch.GetTimestamp() - started;
}

// Calculates throughput, latency percentiles, and managed allocations.
stopwatch.Stop();
var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
var ordersPerSecond = orderCount / stopwatch.Elapsed.TotalSeconds;
Array.Sort(latencies);

// Prints the environment and measured baseline results.
Console.WriteLine("ABADAR Version 0 matching benchmark");
Console.WriteLine($"Runtime: {Environment.Version}");
Console.WriteLine($"OS: {Environment.OSVersion}");
Console.WriteLine($"Processor count: {Environment.ProcessorCount}");
Console.WriteLine($"Orders: {orderCount:N0}");
Console.WriteLine($"Trades: {trades:N0}");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed.TotalMilliseconds:N2} ms");
Console.WriteLine($"Throughput: {ordersPerSecond:N0} orders/sec");
Console.WriteLine($"p50: {Microseconds(latencies, 0.50):N2} µs");
Console.WriteLine($"p95: {Microseconds(latencies, 0.95):N2} µs");
Console.WriteLine($"p99: {Microseconds(latencies, 0.99):N2} µs");
Console.WriteLine($"Allocated: {allocatedBytes / (1024d * 1024d):N2} MiB");

// Creates a consistent limit order for the benchmark workload.
static OrderRequest Limit(OrderSide side, Guid accountId) =>
    new(Guid.NewGuid(), accountId, "BTC/USD", side, OrderType.Limit, 1, 100);

// Converts one percentile's timestamp ticks into microseconds.
static double Microseconds(long[] values, double percentile)
{
    var index = (int)Math.Ceiling(values.Length * percentile) - 1;
    return values[Math.Max(index, 0)] * 1_000_000d / Stopwatch.Frequency;
}