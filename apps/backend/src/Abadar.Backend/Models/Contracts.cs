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

public sealed record ApiError(
    string Code,
    string Message,
    string? RequestId = null,
    IReadOnlyDictionary<string, string[]>? Details = null);

public sealed record ErrorEnvelope(ApiError Error);