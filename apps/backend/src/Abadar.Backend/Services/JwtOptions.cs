namespace Abadar.Backend.Services;

public sealed record JwtOptions(
    string Issuer,
    string Audience,
    string SigningKey,
    TimeSpan Lifetime)
{
    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var signingKey = configuration["JWT_SIGNING_KEY"]
            ?? "abadar-development-signing-key-change-before-production-2026";

        if (signingKey.Length < 32)
        {
            throw new InvalidOperationException("JWT_SIGNING_KEY must be at least 32 characters.");
        }

        var lifetimeMinutes = int.TryParse(
            configuration["JWT_LIFETIME_MINUTES"],
            out var configuredLifetime)
            ? configuredLifetime
            : 60;

        return new JwtOptions(
            configuration["JWT_ISSUER"] ?? "abadar-backend",
            configuration["JWT_AUDIENCE"] ?? "abadar-web",
            signingKey,
            TimeSpan.FromMinutes(Math.Clamp(lifetimeMinutes, 5, 1440)));
    }
}