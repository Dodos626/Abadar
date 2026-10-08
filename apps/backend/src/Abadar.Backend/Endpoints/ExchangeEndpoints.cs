using System.Security.Claims;
using Abadar.Backend.Models;
using Abadar.Backend.Services;

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