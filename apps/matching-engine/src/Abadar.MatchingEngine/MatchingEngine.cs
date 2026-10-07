using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Abadar.MatchingEngine;

// Defines asynchronous submission, cancellation, and snapshot operations.
public interface IMatchingEngine : IAsyncDisposable
{
    // Queues an order for sequential processing by its symbol worker.
    ValueTask<OrderResult> SubmitAsync(
        OrderRequest order,
        CancellationToken cancellationToken = default);

    // Queues cancellation for the worker that owns the symbol.
    ValueTask<bool> CancelAsync(
        string symbol,
        Guid orderId,
        CancellationToken cancellationToken = default);

    // Queues a consistent snapshot request for the symbol worker.
    ValueTask<OrderBookSnapshot> SnapshotAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}

// Symbols are independent. Each symbol gets one sequential channel reader.
public sealed class MatchingEngine : IMatchingEngine
{
    // Stores one independent sequential worker per normalized symbol.
    private readonly ConcurrentDictionary<string, SymbolWorker> _workers = new();

    // Sends an order to the worker responsible for its symbol.
    public ValueTask<OrderResult> SubmitAsync(
        OrderRequest order,
        CancellationToken cancellationToken = default) =>
        Worker(order.Symbol).SubmitAsync(order, cancellationToken);

    // Sends a cancellation to the worker responsible for its symbol.
    public ValueTask<bool> CancelAsync(
        string symbol,
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        Worker(symbol).CancelAsync(orderId, cancellationToken);

    // Requests a snapshot from the worker responsible for its symbol.
    public ValueTask<OrderBookSnapshot> SnapshotAsync(
        string symbol,
        CancellationToken cancellationToken = default) =>
        Worker(symbol).SnapshotAsync(cancellationToken);

    // Completes every symbol worker and waits for its queued work.
    public async ValueTask DisposeAsync()
    {
        foreach (var worker in _workers.Values)
        {
            await worker.DisposeAsync();
        }
    }

    // Returns the existing symbol worker or creates its single owner.
    private SymbolWorker Worker(string symbol)
    {
        var normalized = OrderBook.NormalizeSymbol(symbol);
        return _workers.GetOrAdd(normalized, static value => new SymbolWorker(value));
    }

    // Owns one order book and processes all of its commands sequentially.
    private sealed class SymbolWorker : IAsyncDisposable
    {
        // Buffers commands while allowing only one reader to mutate the book.
        private readonly Channel<Command> _commands = Channel.CreateUnbounded<Command>(
            new UnboundedChannelOptions { SingleReader = true });
        // Tracks the background command-processing loop.
        private readonly Task _loop;

        // Starts the command loop for a new symbol order book.
        public SymbolWorker(string symbol)
        {
            Book = new OrderBook(symbol);
            _loop = RunAsync();
        }

        // Holds the mutable order book exclusively owned by this worker.
        private OrderBook Book { get; }

        // Queues an order command and returns its eventual result.
        public ValueTask<OrderResult> SubmitAsync(OrderRequest order, CancellationToken token) =>
            SendAsync<OrderResult>(complete => new Submit(order, complete), token);

        // Queues a cancellation command and returns whether it removed an order.
        public ValueTask<bool> CancelAsync(Guid orderId, CancellationToken token) =>
            SendAsync<bool>(complete => new Cancel(orderId, complete), token);

        // Queues a snapshot command behind earlier mutations.
        public ValueTask<OrderBookSnapshot> SnapshotAsync(CancellationToken token) =>
            SendAsync<OrderBookSnapshot>(complete => new Snapshot(complete), token);

        // Stops accepting commands and drains the worker loop.
        public async ValueTask DisposeAsync()
        {
            _commands.Writer.TryComplete();
            await _loop;
        }

        // Writes a command to the channel and awaits its typed completion.
        private async ValueTask<T> SendAsync<T>(
            Func<TaskCompletionSource<T>, Command> create,
            CancellationToken token)
        {
            var completion = new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            await _commands.Writer.WriteAsync(create(completion), token);
            return await completion.Task.WaitAsync(token);
        }

        // Reads and executes commands one at a time for deterministic ownership.
        private async Task RunAsync()
        {
            await foreach (var command in _commands.Reader.ReadAllAsync())
            {
                command.Execute(Book);
            }
        }

        // Defines work that can mutate or read the owned order book.
        private abstract record Command
        {
            // Executes this command against the worker's order book.
            public abstract void Execute(OrderBook book);
        }

        // Represents one queued order submission.
        private sealed record Submit(
            OrderRequest Order,
            TaskCompletionSource<OrderResult> Completion) : Command
        {
            // Submits the order and completes the waiting caller.
            public override void Execute(OrderBook book) => Complete(() => book.Submit(Order));

            // Propagates either the submission result or its exception.
            private void Complete(Func<OrderResult> action)
            {
                try { Completion.SetResult(action()); }
                catch (Exception exception) { Completion.SetException(exception); }
            }
        }

        // Represents one queued order cancellation.
        private sealed record Cancel(
            Guid OrderId,
            TaskCompletionSource<bool> Completion) : Command
        {
            // Cancels the order and completes the waiting caller.
            public override void Execute(OrderBook book) => Complete(() => book.Cancel(OrderId));

            // Propagates either the cancellation result or its exception.
            private void Complete(Func<bool> action)
            {
                try { Completion.SetResult(action()); }
                catch (Exception exception) { Completion.SetException(exception); }
            }
        }

        // Represents one queued order-book snapshot request.
        private sealed record Snapshot(
            TaskCompletionSource<OrderBookSnapshot> Completion) : Command
        {
            // Captures the snapshot and completes the waiting caller.
            public override void Execute(OrderBook book) => Complete(book.Snapshot);

            // Propagates either the snapshot result or its exception.
            private void Complete(Func<OrderBookSnapshot> action)
            {
                try { Completion.SetResult(action()); }
                catch (Exception exception) { Completion.SetException(exception); }
            }
        }
    }
}