using System.Security.Claims;
using Abadar.Backend.Data;
using Abadar.Backend.Models;
using Abadar.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Abadar.Backend.Endpoints;

// Maps Version 1 history, simulation, and destructive administration routes.
public static class ExchangeEndpoints
{
    // Registers authenticated history routes and administrator-only mutation routes.
    public static IEndpointRouteBuilder MapExchangeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Returns recent durable orders with optional symbol filtering.
        endpoints.MapGet("/api/v1/orders", async (
            IExchangeService exchangeService,
            string? symbol,
            int? limit,
            CancellationToken cancellationToken) =>
            Results.Ok(await exchangeService.GetOrdersAsync(symbol, limit ?? 100, cancellationToken)))
            .RequireAuthorization();

        // Returns recent immutable trades with optional symbol filtering.
        endpoints.MapGet("/api/v1/trades", async (
            IExchangeService exchangeService,
            string? symbol,
            int? limit,
            CancellationToken cancellationToken) =>
            Results.Ok(await exchangeService.GetTradesAsync(symbol, limit ?? 100, cancellationToken)))
            .RequireAuthorization();

        // Generates durable simulated market activity for administrators.
        endpoints.MapPost("/api/v1/admin/simulations", async (
            SimulateMarketRequest request,
            IExchangeService exchangeService,
            CancellationToken cancellationToken) =>
            Results.Ok(await exchangeService.SimulateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        // Deletes exchange data and non-calling users while preserving the current admin.
        endpoints.MapPost("/api/v1/admin/database/reset", async (
            ClaimsPrincipal principal,
            IExchangeService exchangeService,
            CancellationToken cancellationToken) =>
            Results.Ok(await exchangeService.ResetDatabaseAsync(
                GetUserId(principal),
                cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        // Reports Kafka publication and consumer projection progress.
        endpoints.MapGet("/api/v1/admin/events/status", async (
            IEventReplayService eventReplayService,
            CancellationToken cancellationToken) =>
            Results.Ok(await eventReplayService.GetStatusAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        // Clears one projection and replays its Kafka consumer from the beginning.
        endpoints.MapPost("/api/v1/admin/events/replay/{consumer}", async (
            string consumer,
            IEventReplayService eventReplayService,
            CancellationToken cancellationToken) =>
            Results.Ok(await eventReplayService.ReplayAsync(consumer, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        // Returns eventually consistent positions built by the portfolio consumer.
        endpoints.MapGet("/api/v1/portfolio/positions", async (
            AbadarDbContext dbContext,
            CancellationToken cancellationToken) => Results.Ok(await dbContext.PortfolioPositions
                .AsNoTracking()
                .OrderBy(value => value.AccountId)
                .ThenBy(value => value.Asset)
                .Select(value => new PortfolioPositionResponse(
                    value.AccountId,
                    value.Asset,
                    value.Quantity,
                    value.UpdatedAt))
                .ToListAsync(cancellationToken)))
            .RequireAuthorization();

        // Returns eventually consistent tickers built by the market consumer.
        endpoints.MapGet("/api/v1/market/projections", async (
            AbadarDbContext dbContext,
            CancellationToken cancellationToken) => Results.Ok(await dbContext.MarketProjections
                .AsNoTracking()
                .OrderBy(value => value.Symbol)
                .Select(value => new MarketProjectionResponse(
                    value.Symbol,
                    value.LastPrice,
                    value.Volume,
                    value.TradeCount,
                    value.UpdatedAt))
                .ToListAsync(cancellationToken)))
            .RequireAuthorization();

        // Returns eventually consistent statistics built by the analytics consumer.
        endpoints.MapGet("/api/v1/analytics/projections", async (
            AbadarDbContext dbContext,
            CancellationToken cancellationToken) => Results.Ok(await dbContext.AnalyticsProjections
                .AsNoTracking()
                .OrderBy(value => value.Symbol)
                .Select(value => new AnalyticsProjectionResponse(
                    value.Symbol,
                    value.TradeCount,
                    value.TotalQuantity,
                    value.TotalNotional,
                    value.UpdatedAt))
                .ToListAsync(cancellationToken)))
            .RequireAuthorization();

        return endpoints;
    }

    // Reads the authenticated administrator ID from the JWT principal.
    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The authenticated user ID is invalid.");
    }
}