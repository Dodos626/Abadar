using System.Globalization;
using Abadar.Backend.Data;
using Abadar.Backend.Models;
using Abadar.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Abadar.Backend.Endpoints;

// Maps service metadata, health checks, and the temporary market snapshot.
public static class SystemEndpoints
{
    // Registers public operational endpoints and API discovery links.
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1", (ApplicationMetadata metadata) => Results.Ok(new
        {
            service = metadata.Service,
            version = metadata.Version,
            status = "operational",
            links = new Dictionary<string, string>
            {
                ["liveness"] = "/api/v1/health/live",
                ["readiness"] = "/api/v1/health/ready",
                ["markets"] = "/api/v1/markets",
                ["login"] = "/api/v1/auth/login",
                ["users"] = "/api/v1/users",
                ["orders"] = "/api/v1/orders",
                ["trades"] = "/api/v1/trades",
                ["admin_simulations"] = "/api/v1/admin/simulations",
                ["admin_event_status"] = "/api/v1/admin/events/status",
                ["portfolio"] = "/api/v1/portfolio/positions",
                ["market_projection"] = "/api/v1/market/projections",
                ["analytics"] = "/api/v1/analytics/projections",
                ["users_hub"] = "/hubs/users"
            }
        }));

        endpoints.MapGet("/api/v1/health/live", (ApplicationMetadata metadata) =>
            Results.Ok(CreateHealthResponse(metadata)));

        endpoints.MapGet("/api/v1/health/ready", async (
            ApplicationMetadata metadata,
            AbadarDbContext dbContext,
            KafkaOptions kafkaOptions,
            KafkaRuntimeState kafkaRuntimeState,
            CancellationToken cancellationToken) =>
        {
            var databaseReady = await dbContext.Database.CanConnectAsync(cancellationToken);
            var checks = new Dictionary<string, string>
            {
                ["http_server"] = "up",
                ["postgresql"] = databaseReady ? "up" : "down",
                ["kafka"] = !kafkaOptions.Enabled
                    ? "disabled"
                    : kafkaRuntimeState.TopicsReady ? "up" : "starting"
            };

            return databaseReady
                ? Results.Ok(CreateHealthResponse(metadata, checks))
                : Results.Json(
                    CreateHealthResponse(metadata, checks) with { Status = "unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        endpoints.MapGet("/api/v1/markets", () => Results.Ok(new MarketsResponse(
        [
            new("BTC/USD", "BTC", "USD", "100125.40", "+2.84", "1842.72", "TRADING"),
            new("ETH/USD", "ETH", "USD", "3294.18", "+1.37", "24817.09", "TRADING"),
            new("SOL/USD", "SOL", "USD", "142.61", "-0.62", "109384.44", "TRADING")
        ])));

        return endpoints;
    }

    // Builds a consistent health response with calculated process uptime.
    private static HealthResponse CreateHealthResponse(
        ApplicationMetadata metadata,
        IReadOnlyDictionary<string, string>? checks = null)
    {
        var uptimeSeconds = Math.Max(
            0,
            (long)Math.Floor((DateTimeOffset.UtcNow - metadata.StartedAt).TotalSeconds));

        return new HealthResponse(
            "ok",
            metadata.Service,
            metadata.Version,
            DateTimeOffset.UtcNow,
            uptimeSeconds.ToString(CultureInfo.InvariantCulture) + "s",
            checks);
    }
}