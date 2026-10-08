using System.Text.Json.Serialization;

namespace Abadar.Backend.Models;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserResponse User);

public sealed record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string Password,
    string Role);

public sealed record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string? Password,
    string Role);

public sealed record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string? Password);

public sealed record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    DateTimeOffset? LastOnline,
    string Email,
    string Username,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ApplicationMetadata(
    string Service,
    string Version,
    DateTimeOffset StartedAt);

public sealed record HealthResponse(
    string Status,
    string Service,
    string Version,
    DateTimeOffset Timestamp,
    string Uptime,
    IReadOnlyDictionary<string, string>? Checks = null);

public sealed record Market(
    string Symbol,
    string BaseAsset,
    string QuoteAsset,
    string LastPrice,
    [property: JsonPropertyName("change_percent_24h")]
    string ChangePercent24H,
    [property: JsonPropertyName("volume_24h")]
    string Volume24H,
    string Status);

public sealed record MarketsResponse(IReadOnlyList<Market> Data);

// Defines administrator-controlled random market generation parameters.
public sealed record SimulateMarketRequest(
    IReadOnlyList<string> Symbols,
    int MinTrades,
    int MaxTrades,
    decimal MinPrice,
    decimal MaxPrice,
    decimal MinQuantity,
    decimal MaxQuantity,
    int SellPercentage,
    int MarketOrderPercentage,
    int? Seed = null,
    Guid? SimulationId = null);

// Streams one real-time checkpoint from the simulation pipeline.
public sealed record SimulationProgressUpdate(
    Guid SimulationId,
    string Phase,
    string Message,
    string? Symbol,
    int CurrentSymbol,
    int TotalSymbols,
    int OrdersGenerated,
    int OrdersProcessed,
    int TotalOrders,
    int TradesExecuted,
    decimal ExecutedQuantity,
    decimal Percent,
    DateTimeOffset OccurredAt);

// Summarizes one symbol produced by a completed simulation request.
public sealed record SimulationSymbolResult(
    string Symbol,
    int OrdersSubmitted,
    int TradesExecuted,
    decimal ExecutedQuantity,
    decimal? LastPrice);

// Returns aggregate and per-symbol simulation results to the administrator.
public sealed record SimulateMarketResponse(
    int OrdersSubmitted,
    int TradesExecuted,
    IReadOnlyList<SimulationSymbolResult> Symbols);

// Exposes one durable order without leaking EF Core entities.
public sealed record OrderHistoryResponse(
    Guid Id,
    Guid AccountId,
    string Symbol,
    string Side,
    string Type,
    decimal? Price,
    decimal Quantity,
    decimal RemainingQuantity,
    string Status,
    long Sequence,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// Exposes one immutable durable trade to authenticated clients.
public sealed record TradeHistoryResponse(
    Guid Id,
    string Symbol,
    Guid BuyOrderId,
    Guid SellOrderId,
    decimal Price,
    decimal Quantity,
    long Sequence,
    DateTimeOffset ExecutedAt);

// Reports the records removed by an administrator database reset.
public sealed record ResetDatabaseResponse(
    Guid PreservedAdminId,
    int UsersDeleted,
    int OrdersDeleted,
    int TradesDeleted);

// Reports one consumer projection's processed-event count.
public sealed record ConsumerProgressResponse(string Consumer, int ProcessedEvents);

// Reports Kafka, outbox, and consumer projection health to administrators.
public sealed record EventSystemStatusResponse(
    bool KafkaEnabled,
    bool TopicsReady,
    bool PublisherConnected,
    string? LastError,
    int OutboxPending,
    int OutboxPublished,
    IReadOnlyList<ConsumerProgressResponse> Consumers);

// Reports a requested projection replay reset.
public sealed record EventReplayResponse(
    string Consumer,
    int ProcessedEventsCleared,
    int EventsReplayed,
    DateTimeOffset RequestedAt);

// Exposes one eventually consistent portfolio position.
public sealed record PortfolioPositionResponse(
    Guid AccountId,
    string Asset,
    decimal Quantity,
    DateTimeOffset UpdatedAt);

// Exposes one eventually consistent market ticker projection.
public sealed record MarketProjectionResponse(
    string Symbol,
    decimal LastPrice,
    decimal Volume,
    long TradeCount,
    DateTimeOffset UpdatedAt);

// Exposes one eventually consistent analytics projection.
public sealed record AnalyticsProjectionResponse(
    string Symbol,
    long TradeCount,
    decimal TotalQuantity,
    decimal TotalNotional,
    DateTimeOffset UpdatedAt);

public sealed record ApiError(
    string Code,
    string Message,
    string? RequestId = null,
    IReadOnlyDictionary<string, string[]>? Details = null);

public sealed record ErrorEnvelope(ApiError Error);