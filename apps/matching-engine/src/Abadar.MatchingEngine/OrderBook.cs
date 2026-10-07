namespace Abadar.MatchingEngine;

// One worker owns an OrderBook, so this class intentionally has no locks.
public sealed class OrderBook
{
    // Keeps bid prices ordered from highest to lowest.
    private readonly SortedDictionary<decimal, LinkedList<Order>> _bids =
        new(Comparer<decimal>.Create((left, right) => right.CompareTo(left)));
    // Keeps ask prices ordered from lowest to highest.
    private readonly SortedDictionary<decimal, LinkedList<Order>> _asks = new();
    // Locates open orders directly for efficient cancellation.
    private readonly Dictionary<Guid, OrderLocation> _orders = [];
    // Prevents an order ID from being submitted more than once.
    private readonly HashSet<Guid> _seenOrderIds = [];
    // Assigns deterministic sequence numbers to accepted orders.
    private long _nextSequence;
    // Assigns deterministic sequence numbers to executions.
    private long _nextExecutionSequence;

    // Creates an empty order book for one normalized trading symbol.
    public OrderBook(string symbol)
    {
        Symbol = NormalizeSymbol(symbol);
    }

    // Identifies the only trading symbol accepted by this book.
    public string Symbol { get; }

    // Matches a new order and rests any remaining limit quantity.
    public OrderResult Submit(OrderRequest request)
    {
        Validate(request);
        _seenOrderIds.Add(request.Id);

        var order = new Order(request, ++_nextSequence);
        var executions = new List<Execution>();
        var opposite = order.Side == OrderSide.Buy ? _asks : _bids;

        while (order.RemainingQuantity > 0 && opposite.Count > 0)
        {
            var bestLevel = opposite.First();
            if (!Crosses(order, bestLevel.Key))
            {
                break;
            }

            MatchLevel(order, bestLevel.Value, executions);
            if (bestLevel.Value.Count == 0)
            {
                opposite.Remove(bestLevel.Key);
            }
        }

        if (order.RemainingQuantity == 0)
        {
            order.Status = OrderStatus.Filled;
        }
        else if (order.Type == OrderType.Market)
        {
            // Unfilled market quantity never rests on the book.
            order.Status = OrderStatus.Cancelled;
        }
        else
        {
            order.Status = executions.Count == 0
                ? OrderStatus.Open
                : OrderStatus.PartiallyFilled;
            Add(order);
        }

        return new OrderResult(
            order.Id,
            order.Sequence,
            order.Status,
            order.RemainingQuantity,
            executions);
    }

    // Removes an open order and returns false when it is already absent.
    public bool Cancel(Guid orderId)
    {
        if (!_orders.Remove(orderId, out var location))
        {
            return false;
        }

        location.Level.Remove(location.Node);
        location.Node.Value.Status = OrderStatus.Cancelled;
        if (location.Level.Count == 0)
        {
            location.Book.Remove(location.Node.Value.Price!.Value);
        }

        return true;
    }

    // Creates an immutable view of the current bids and asks.
    public OrderBookSnapshot Snapshot() => new(
        Symbol,
        Snapshot(_bids),
        Snapshot(_asks));

    // Matches an incoming order against one FIFO price level.
    private void MatchLevel(
        Order incoming,
        LinkedList<Order> level,
        List<Execution> executions)
    {
        while (incoming.RemainingQuantity > 0 && level.First is not null)
        {
            var restingNode = level.First;
            var resting = restingNode.Value;
            var quantity = Math.Min(incoming.RemainingQuantity, resting.RemainingQuantity);

            incoming.RemainingQuantity -= quantity;
            resting.RemainingQuantity -= quantity;
            executions.Add(new Execution(
                incoming.Side == OrderSide.Buy ? incoming.Id : resting.Id,
                incoming.Side == OrderSide.Sell ? incoming.Id : resting.Id,
                Symbol,
                resting.Price!.Value,
                quantity,
                ++_nextExecutionSequence));

            if (resting.RemainingQuantity == 0)
            {
                resting.Status = OrderStatus.Filled;
                level.RemoveFirst();
                _orders.Remove(resting.Id);
            }
            else
            {
                resting.Status = OrderStatus.PartiallyFilled;
            }
        }
    }

    // Appends an unmatched limit order to its price level.
    private void Add(Order order)
    {
        var book = order.Side == OrderSide.Buy ? _bids : _asks;
        var price = order.Price!.Value;
        if (!book.TryGetValue(price, out var level))
        {
            level = new LinkedList<Order>();
            book.Add(price, level);
        }

        // Append preserves FIFO inside one price level.
        var node = level.AddLast(order);
        _orders.Add(order.Id, new OrderLocation(book, level, node));
    }

    // Checks whether an incoming order may trade at the opposite price.
    private static bool Crosses(Order order, decimal oppositePrice) =>
        order.Type == OrderType.Market
        || order.Side == OrderSide.Buy && order.Price >= oppositePrice
        || order.Side == OrderSide.Sell && order.Price <= oppositePrice;

    // Converts mutable price levels into immutable snapshot records.
    private static IReadOnlyList<PriceLevel> Snapshot(
        SortedDictionary<decimal, LinkedList<Order>> book) =>
        book.Select(level => new PriceLevel(
                level.Key,
                level.Value.Sum(order => order.RemainingQuantity),
                level.Value.Select(order => new RestingOrder(
                    order.Id,
                    order.Sequence,
                    level.Key,
                    order.RemainingQuantity)).ToArray()))
            .ToArray();

    // Rejects malformed, mismatched, or duplicate order submissions.
    private void Validate(OrderRequest request)
    {
        if (request.Id == Guid.Empty || request.AccountId == Guid.Empty)
            throw new ArgumentException("Order and account IDs are required.");
        if (!string.Equals(NormalizeSymbol(request.Symbol), Symbol, StringComparison.Ordinal))
            throw new ArgumentException($"Order symbol must be {Symbol}.", nameof(request));
        if (request.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Quantity must be positive.");
        if (request.Type == OrderType.Limit && request.Price is null or <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "A limit price must be positive.");
        if (request.Type == OrderType.Market && request.Price is not null)
            throw new ArgumentException("A market order cannot have a price.", nameof(request));
        if (_seenOrderIds.Contains(request.Id))
            throw new InvalidOperationException("An order with this ID was already submitted.");
    }

    // Trims and uppercases a required trading symbol.
    internal static string NormalizeSymbol(string symbol) =>
        string.IsNullOrWhiteSpace(symbol)
            ? throw new ArgumentException("A symbol is required.", nameof(symbol))
            : symbol.Trim().ToUpperInvariant();

    // Stores the mutable state of an order while the book owns it.
    private sealed class Order(OrderRequest request, long sequence)
    {
        // Identifies this order for matching and cancellation.
        public Guid Id { get; } = request.Id;
        // Determines whether this order buys or sells.
        public OrderSide Side { get; } = request.Side;
        // Determines whether this order has a price limit.
        public OrderType Type { get; } = request.Type;
        // Stores the limit price or null for a market order.
        public decimal? Price { get; } = request.Price;
        // Tracks the quantity that has not yet executed.
        public decimal RemainingQuantity { get; set; } = request.Quantity;
        // Preserves deterministic arrival order.
        public long Sequence { get; } = sequence;
        // Tracks the current lifecycle state of the order.
        public OrderStatus Status { get; set; }
    }

    // Keeps the references needed to cancel an order in constant time.
    private sealed record OrderLocation(
        SortedDictionary<decimal, LinkedList<Order>> Book,
        LinkedList<Order> Level,
        LinkedListNode<Order> Node);
}