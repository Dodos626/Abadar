using Abadar.Backend.Data;
using Abadar.Backend.Events;
using Abadar.Backend.Hubs;
using Abadar.Backend.Models;
using Abadar.MatchingEngine;
using Microsoft.EntityFrameworkCore;

namespace Abadar.Backend.Services;

// Defines durable exchange recovery, simulation, history, and reset operations.
public interface IExchangeService
{
    // Rebuilds in-memory books and sequence counters from PostgreSQL state.
    Task RecoverAsync(CancellationToken cancellationToken = default);
    // Generates and durably processes administrator-configured market activity.
    Task<SimulateMarketResponse> SimulateAsync(
        SimulateMarketRequest request,
        CancellationToken cancellationToken);
    // Returns recent durable order history with optional symbol filtering.
    Task<IReadOnlyList<OrderHistoryResponse>> GetOrdersAsync(
        string? symbol,
        int limit,
        CancellationToken cancellationToken);
    // Returns recent immutable trade history with optional symbol filtering.
    Task<IReadOnlyList<TradeHistoryResponse>> GetTradesAsync(
        string? symbol,
        int limit,
        CancellationToken cancellationToken);
    // Deletes all exchange data and users except the authenticated administrator.
    Task<ResetDatabaseResponse> ResetDatabaseAsync(
        Guid adminId,
        CancellationToken cancellationToken);
}

// Coordinates matching-engine mutations with their durable EF Core representation.
public sealed class ExchangeService(
    AbadarDbContext dbContext,
    IMatchingEngine matchingEngine,
    ISimulationProgressEvents simulationProgress,
    ILogger<ExchangeService> logger) : IExchangeService
{
    private const int PersistenceBatchSize = 250;
    private const int MaximumOrdersPerSymbol = 100_000;
    // Serializes simulations and destructive resets against the shared matching engine.
    private static readonly SemaphoreSlim MutationGate = new(1, 1);
    // Restricts simulations to the symbols currently supported by the admin UI.
    private static readonly HashSet<string> SupportedSymbols =
        new(StringComparer.OrdinalIgnoreCase) { "BTC/USD", "ETH/USD", "SOL/USD", "ABR/USD" };

    // Restores active books plus the highest durable order and execution sequences.
    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        var durableOrders = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Status == "open" || order.Status == "partially_filled")
            .OrderBy(order => order.Symbol)
            .ThenBy(order => order.Sequence)
            .ToListAsync(cancellationToken);

        var orderSequences = await dbContext.Orders
            .AsNoTracking()
            .GroupBy(order => order.Symbol)
            .Select(group => new { Symbol = group.Key, Sequence = group.Max(order => order.Sequence) })
            .ToDictionaryAsync(value => value.Symbol, value => value.Sequence, cancellationToken);
        var executionSequences = await dbContext.Trades
            .AsNoTracking()
            .GroupBy(trade => trade.Symbol)
            .Select(group => new { Symbol = group.Key, Sequence = group.Max(trade => trade.Sequence) })
            .ToDictionaryAsync(value => value.Symbol, value => value.Sequence, cancellationToken);

        var durableBySymbol = durableOrders
            .GroupBy(order => order.Symbol)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var symbols = orderSequences.Keys
            .Concat(executionSequences.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var symbol in symbols)
        {
            var recovered = durableBySymbol.GetValueOrDefault(symbol, [])
                .Select(order => new RecoveredOrder(
                new OrderRequest(
                    order.Id,
                    order.AccountId,
                    order.Symbol,
                    ParseSide(order.Side),
                    ParseType(order.Type),
                    order.Quantity,
                    order.Price),
                order.Sequence,
                order.RemainingQuantity,
                ParseStatus(order.Status))).ToArray();

            await matchingEngine.RecoverAsync(
                symbol,
                recovered,
                orderSequences.GetValueOrDefault(symbol),
                executionSequences.GetValueOrDefault(symbol),
                cancellationToken);
        }

        logger.LogInformation("Recovered {OrderCount} open orders across {SymbolCount} symbols",
            durableOrders.Count,
            durableOrders.Select(order => order.Symbol).Distinct().Count());
    }

    // Generates randomized orders, processes them sequentially, and persists every result.
    public async Task<SimulateMarketResponse> SimulateAsync(
        SimulateMarketRequest request,
        CancellationToken cancellationToken)
    {
        await MutationGate.WaitAsync(cancellationToken);
        var autoDetectChanges = dbContext.ChangeTracker.AutoDetectChangesEnabled;
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
        var simulationId = request.SimulationId ?? Guid.NewGuid();
        try
        {
            ValidateSimulation(request);
            var symbols = request.Symbols
                .Select(NormalizeSymbol)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var random = request.Seed is null ? Random.Shared : new Random(request.Seed.Value);
            var symbolResults = new List<SimulationSymbolResult>();
            var symbolOrders = new List<(string Symbol, List<OrderRequest> Orders)>();
            var durableOrders = await dbContext.Orders
                .Where(order => symbols.Contains(order.Symbol)
                    && (order.Status == "open" || order.Status == "partially_filled"))
                .ToDictionaryAsync(order => order.Id, cancellationToken);

            await ReportProgressAsync(
                simulationId,
                "validating",
                "Validated ranges, symbols, percentages, and the deterministic seed.",
                null,
                0,
                symbols.Length,
                0,
                0,
                0,
                0,
                0,
                4,
                cancellationToken);

            for (var symbolIndex = 0; symbolIndex < symbols.Length; symbolIndex++)
            {
                var symbol = symbols[symbolIndex];
                var orderCount = random.Next(request.MinTrades, request.MaxTrades + 1);
                var orders = new List<OrderRequest>(orderCount);

                for (var index = 0; index < orderCount; index++)
                {
                    var side = random.Next(100) < request.SellPercentage
                        ? OrderSide.Sell
                        : OrderSide.Buy;
                    var type = random.Next(100) < request.MarketOrderPercentage
                        ? OrderType.Market
                        : OrderType.Limit;
                    decimal? price = type == OrderType.Limit
                        ? NextDecimal(random, request.MinPrice, request.MaxPrice)
                        : null;

                    orders.Add(new OrderRequest(
                        Guid.NewGuid(),
                        SimulationAccountId(symbol, side),
                        symbol,
                        side,
                        type,
                        NextDecimal(random, request.MinQuantity, request.MaxQuantity),
                        price));
                }

                // Submit priced liquidity first, then market orders, while retaining random side distribution.
                orders.Sort((left, right) => left.Type.CompareTo(right.Type));
                symbolOrders.Add((symbol, orders));
            }

            var totalOrders = symbolOrders.Sum(value => value.Orders.Count);
            var generatedOrders = 0;
            for (var symbolIndex = 0; symbolIndex < symbolOrders.Count; symbolIndex++)
            {
                var item = symbolOrders[symbolIndex];
                generatedOrders += item.Orders.Count;
                await ReportProgressAsync(
                    simulationId,
                    "generated",
                    $"Generated {item.Orders.Count:N0} orders for {item.Symbol} and sorted liquidity before market orders.",
                    item.Symbol,
                    symbolIndex + 1,
                    symbolOrders.Count,
                    generatedOrders,
                    0,
                    totalOrders,
                    0,
                    0,
                    8 + 12m * generatedOrders / Math.Max(totalOrders, 1),
                    cancellationToken);
            }

            var processedOrders = 0;
            var totalTradesExecuted = 0;
            var totalExecutedQuantity = 0m;
            for (var symbolIndex = 0; symbolIndex < symbolOrders.Count; symbolIndex++)
            {
                var (symbol, orders) = symbolOrders[symbolIndex];
                var tradesExecuted = 0;
                var executedQuantity = 0m;
                decimal? lastPrice = null;
                for (var index = 0; index < orders.Count; index++)
                {
                    var result = await StageDurableOrderAsync(
                        orders[index],
                        durableOrders,
                        cancellationToken);
                    tradesExecuted += result.Executions.Count;
                    executedQuantity += result.Executions.Sum(execution => execution.Quantity);
                    lastPrice = result.Executions.LastOrDefault()?.Price ?? lastPrice;
                    processedOrders++;
                    totalTradesExecuted += result.Executions.Count;
                    totalExecutedQuantity += result.Executions.Sum(execution => execution.Quantity);

                    if ((index + 1) % PersistenceBatchSize == 0)
                    {
                        await FlushPersistenceBatchAsync(cancellationToken);
                        await ReportProgressAsync(
                            simulationId,
                            "persisting",
                            $"Matched and committed {processedOrders:N0} of {totalOrders:N0} orders in PostgreSQL batches.",
                            symbol,
                            symbolIndex + 1,
                            symbolOrders.Count,
                            totalOrders,
                            processedOrders,
                            totalOrders,
                            totalTradesExecuted,
                            totalExecutedQuantity,
                            20 + 65m * processedOrders / Math.Max(totalOrders, 1),
                            cancellationToken);
                    }
                }

                if (orders.Count % PersistenceBatchSize != 0)
                {
                    await FlushPersistenceBatchAsync(cancellationToken);
                }

                await ReportProgressAsync(
                    simulationId,
                    "symbol_complete",
                    $"Completed {symbol}: {orders.Count:N0} orders produced {tradesExecuted:N0} executions.",
                    symbol,
                    symbolIndex + 1,
                    symbolOrders.Count,
                    totalOrders,
                    processedOrders,
                    totalOrders,
                    totalTradesExecuted,
                    totalExecutedQuantity,
                    20 + 65m * processedOrders / Math.Max(totalOrders, 1),
                    cancellationToken);

                symbolResults.Add(new SimulationSymbolResult(
                    symbol,
                    orders.Count,
                    tradesExecuted,
                    executedQuantity,
                    lastPrice));
            }

            await ReportProgressAsync(
                simulationId,
                "completed",
                "Matching and durable writes completed; the outbox publisher will now deliver events to Kafka.",
                null,
                symbolOrders.Count,
                symbolOrders.Count,
                totalOrders,
                totalOrders,
                totalOrders,
                totalTradesExecuted,
                totalExecutedQuantity,
                100,
                cancellationToken);

            return new SimulateMarketResponse(
                symbolResults.Sum(result => result.OrdersSubmitted),
                symbolResults.Sum(result => result.TradesExecuted),
                symbolResults);
        }
        catch
        {
            await ReportProgressAsync(
                simulationId,
                "failed",
                "The simulation failed; tracked changes were cleared and the in-memory books were recovered from durable state.",
                null,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                100,
                CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            await matchingEngine.ResetAsync(CancellationToken.None);
            await RecoverAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
            MutationGate.Release();
        }
    }

    // Publishes one best-effort real-time checkpoint without affecting simulation correctness.
    private async Task ReportProgressAsync(
        Guid simulationId,
        string phase,
        string message,
        string? symbol,
        int currentSymbol,
        int totalSymbols,
        int ordersGenerated,
        int ordersProcessed,
        int totalOrders,
        int tradesExecuted,
        decimal executedQuantity,
        decimal percent,
        CancellationToken cancellationToken)
    {
        try
        {
            await simulationProgress.PublishAsync(new SimulationProgressUpdate(
                simulationId,
                phase,
                message,
                symbol,
                currentSymbol,
                totalSymbols,
                ordersGenerated,
                ordersProcessed,
                totalOrders,
                tradesExecuted,
                executedQuantity,
                decimal.Round(Math.Clamp(percent, 0, 100), 2),
                DateTimeOffset.UtcNow), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Could not publish simulation progress for {SimulationId}", simulationId);
        }
    }

    // Projects bounded order history without tracking database entities.
    public async Task<IReadOnlyList<OrderHistoryResponse>> GetOrdersAsync(
        string? symbol,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Orders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var normalized = NormalizeSymbol(symbol);
            query = query.Where(order => order.Symbol == normalized);
        }

        return await query
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Sequence)
            .Take(NormalizeLimit(limit))
            .Select(order => new OrderHistoryResponse(
                order.Id,
                order.AccountId,
                order.Symbol,
                order.Side,
                order.Type,
                order.Price,
                order.Quantity,
                order.RemainingQuantity,
                order.Status,
                order.Sequence,
                order.CreatedAt,
                order.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    // Projects bounded immutable trade history without tracking database entities.
    public async Task<IReadOnlyList<TradeHistoryResponse>> GetTradesAsync(
        string? symbol,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Trades.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var normalized = NormalizeSymbol(symbol);
            query = query.Where(trade => trade.Symbol == normalized);
        }

        return await query
            .OrderByDescending(trade => trade.ExecutedAt)
            .ThenByDescending(trade => trade.Sequence)
            .Take(NormalizeLimit(limit))
            .Select(trade => new TradeHistoryResponse(
                trade.Id,
                trade.Symbol,
                trade.BuyOrderId,
                trade.SellOrderId,
                trade.Price,
                trade.Quantity,
                trade.Sequence,
                trade.ExecutedAt))
            .ToListAsync(cancellationToken);
    }

    // Preserves the caller while clearing users, orders, trades, and active books.
    public async Task<ResetDatabaseResponse> ResetDatabaseAsync(
        Guid adminId,
        CancellationToken cancellationToken)
    {
        await MutationGate.WaitAsync(cancellationToken);
        try
        {
            var preservedAdmin = await dbContext.Users.SingleOrDefaultAsync(
                user => user.Id == adminId && user.Role == UserRole.Admin,
                cancellationToken);
            if (preservedAdmin is null)
            {
                throw new ApiConflictException(
                    "admin_account_required",
                    "The authenticated administrator account could not be preserved.");
            }

            var ordersDeleted = await dbContext.Orders.CountAsync(cancellationToken);
            var tradesDeleted = await dbContext.Trades.CountAsync(cancellationToken);
            var users = await dbContext.Users
                .Where(user => user.Id != adminId)
                .ToListAsync(cancellationToken);

            var trades = await dbContext.Trades.ToListAsync(cancellationToken);
            var orders = await dbContext.Orders.ToListAsync(cancellationToken);
            var outboxEvents = await dbContext.OutboxEvents.ToListAsync(cancellationToken);
            var processedEvents = await dbContext.ProcessedEvents.ToListAsync(cancellationToken);
            var positions = await dbContext.PortfolioPositions.ToListAsync(cancellationToken);
            var marketProjections = await dbContext.MarketProjections.ToListAsync(cancellationToken);
            var analyticsProjections = await dbContext.AnalyticsProjections.ToListAsync(cancellationToken);
            dbContext.Trades.RemoveRange(trades);
            dbContext.Orders.RemoveRange(orders);
            dbContext.OutboxEvents.RemoveRange(outboxEvents);
            dbContext.ProcessedEvents.RemoveRange(processedEvents);
            dbContext.PortfolioPositions.RemoveRange(positions);
            dbContext.MarketProjections.RemoveRange(marketProjections);
            dbContext.AnalyticsProjections.RemoveRange(analyticsProjections);
            dbContext.Users.RemoveRange(users);
            await dbContext.SaveChangesAsync(cancellationToken);
            await matchingEngine.ResetAsync(cancellationToken);

            logger.LogWarning(
                "Administrator {AdminId} reset the database; removed {UserCount} users, {OrderCount} orders, and {TradeCount} trades",
                adminId,
                users.Count,
                ordersDeleted,
                tradesDeleted);

            return new ResetDatabaseResponse(adminId, users.Count, ordersDeleted, tradesDeleted);
        }
        finally
        {
            MutationGate.Release();
        }
    }

    // Stages one engine submission for the next batched database commit.
    private async Task<OrderResult> StageDurableOrderAsync(
        OrderRequest request,
        IDictionary<Guid, ExchangeOrder> durableOrders,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var result = await matchingEngine.SubmitAsync(request, cancellationToken);
        var incoming = new ExchangeOrder
        {
            Id = request.Id,
            AccountId = request.AccountId,
            Symbol = NormalizeSymbol(request.Symbol),
            Side = Format(request.Side),
            Type = Format(request.Type),
            Price = request.Price,
            Quantity = request.Quantity,
            RemainingQuantity = result.RemainingQuantity,
            Status = Format(result.Status),
            Sequence = result.Sequence,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Orders.Add(incoming);
        durableOrders[incoming.Id] = incoming;
        dbContext.OutboxEvents.Add(IntegrationEventFactory.Create(
            new EventEnvelope<OrderAcceptedEvent>(
                Guid.NewGuid(),
                EventCatalog.OrderAcceptedV1,
                now,
                request.Id.ToString(),
                "order",
                result.Sequence,
                1,
                new OrderAcceptedEvent(
                    request.Id,
                    request.AccountId,
                    incoming.Symbol,
                    incoming.Side,
                    incoming.Type,
                    incoming.Price,
                    incoming.Quantity,
                    incoming.RemainingQuantity,
                    incoming.Status)),
            EventCatalog.OrdersTopic,
            incoming.Symbol));

        var restingOrderIds = result.Executions
            .Select(execution => request.Side == OrderSide.Buy ? execution.SellOrderId : execution.BuyOrderId)
            .Distinct()
            .ToArray();
        foreach (var restingOrderId in restingOrderIds)
        {
            if (!durableOrders.TryGetValue(restingOrderId, out var restingOrder))
            {
                throw new InvalidOperationException("A matched resting order was not found in durable state.");
            }
            if (dbContext.Entry(restingOrder).State == EntityState.Detached)
            {
                dbContext.Orders.Update(restingOrder);
            }
        }

        foreach (var execution in result.Executions)
        {
            var restingId = request.Side == OrderSide.Buy
                ? execution.SellOrderId
                : execution.BuyOrderId;
            if (!durableOrders.TryGetValue(restingId, out var resting))
            {
                throw new InvalidOperationException("A matched resting order was not found in durable state.");
            }

            resting.RemainingQuantity -= execution.Quantity;
            resting.Status = resting.RemainingQuantity == 0 ? "filled" : "partially_filled";
            resting.UpdatedAt = now;
        }

        foreach (var execution in result.Executions)
        {
            var trade = new ExchangeTrade
            {
                Symbol = execution.Symbol,
                BuyOrderId = execution.BuyOrderId,
                SellOrderId = execution.SellOrderId,
                Price = execution.Price,
                Quantity = execution.Quantity,
                Sequence = execution.Sequence,
                ExecutedAt = now
            };
            dbContext.Trades.Add(trade);

            var buyAccountId = execution.BuyOrderId == request.Id
                ? request.AccountId
                : durableOrders[execution.BuyOrderId].AccountId;
            var sellAccountId = execution.SellOrderId == request.Id
                ? request.AccountId
                : durableOrders[execution.SellOrderId].AccountId;
            dbContext.OutboxEvents.Add(IntegrationEventFactory.Create(
                new EventEnvelope<TradeExecutedEvent>(
                    Guid.NewGuid(),
                    EventCatalog.TradeExecutedV1,
                    now,
                    trade.Id.ToString(),
                    "trade",
                    trade.Sequence,
                    1,
                    new TradeExecutedEvent(
                        trade.Id,
                        trade.Symbol,
                        trade.BuyOrderId,
                        trade.SellOrderId,
                        buyAccountId,
                        sellAccountId,
                        trade.Price,
                        trade.Quantity)),
                EventCatalog.TradesTopic,
                trade.Symbol));
        }

        return result;
    }

    // Commits a bounded batch and clears tracking to keep large simulations efficient.
    private async Task FlushPersistenceBatchAsync(CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.DetectChanges();
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    // Rejects unsupported symbols and unsafe simulation ranges before mutation starts.
    private static void ValidateSimulation(SimulateMarketRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Symbols is null || request.Symbols.Count == 0)
            errors["symbols"] = ["Select at least one trading symbol."];
        else if (request.Symbols.Any(symbol => !SupportedSymbols.Contains(symbol.Trim())))
            errors["symbols"] = ["Supported symbols are BTC/USD, ETH/USD, SOL/USD, and ABR/USD."];
        if (request.MinTrades is < 1 or > MaximumOrdersPerSymbol
            || request.MaxTrades < request.MinTrades
            || request.MaxTrades > MaximumOrdersPerSymbol)
            errors["trades"] = [$"Order counts must be between 1 and {MaximumOrdersPerSymbol:N0} per symbol, with max greater than or equal to min."];
        if (request.MinPrice <= 0 || request.MaxPrice < request.MinPrice)
            errors["price"] = ["Prices must be positive, with max greater than or equal to min."];
        if (request.MinQuantity <= 0 || request.MaxQuantity < request.MinQuantity)
            errors["quantity"] = ["Quantities must be positive, with max greater than or equal to min."];
        if (request.SellPercentage is < 0 or > 100)
            errors["sell_percentage"] = ["Sell percentage must be between 0 and 100."];
        if (request.MarketOrderPercentage is < 0 or > 100)
            errors["market_order_percentage"] = ["Market-order percentage must be between 0 and 100."];
        if (errors.Count > 0)
            throw new ApiValidationException(errors);
    }

    // Applies a safe default and upper bound to history query sizes.
    private static int NormalizeLimit(int limit) => Math.Clamp(limit == 0 ? 100 : limit, 1, 500);

    // Trims and uppercases a trading symbol for consistent storage and routing.
    private static string NormalizeSymbol(string symbol) => symbol.Trim().ToUpperInvariant();

    // Produces a rounded random decimal inside the configured inclusive range.
    private static decimal NextDecimal(Random random, decimal minimum, decimal maximum) =>
        decimal.Round(minimum + (decimal)random.NextDouble() * (maximum - minimum), 8);

    // Derives stable synthetic account IDs for each symbol and order side.
    private static Guid SimulationAccountId(string symbol, OrderSide side)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"ABADAR-SIM:{symbol}:{side}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    // Converts enum names to snake_case values stored by the Version 1 schema.
    private static string Format<T>(T value) where T : struct, Enum
    {
        var text = value.ToString();
        var characters = new List<char>(text.Length + 4);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (index > 0 && char.IsUpper(character))
            {
                characters.Add('_');
            }
            characters.Add(char.ToLowerInvariant(character));
        }
        return new string(characters.ToArray());
    }

    // Parses a durable side string back into the matching-engine enum.
    private static OrderSide ParseSide(string value) =>
        Enum.Parse<OrderSide>(value, true);

    // Parses a durable order-type string back into the matching-engine enum.
    private static OrderType ParseType(string value) =>
        Enum.Parse<OrderType>(value, true);

    // Parses a durable snake_case lifecycle status back into the engine enum.
    private static OrderStatus ParseStatus(string value) =>
        Enum.Parse<OrderStatus>(value.Replace("_", string.Empty), true);
}