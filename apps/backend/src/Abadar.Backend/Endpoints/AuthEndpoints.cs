using Abadar.Backend.Models;
using Abadar.Backend.Services;

namespace Abadar.Backend.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/auth/login", async (
            LoginRequest request,
            IUserService userService,
            ITokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username)
                || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new ErrorEnvelope(new ApiError(
                    "validation_failed",
                    "Username and password are required.")));
            }

            var user = await userService.AuthenticateAsync(
                request.Username,
                request.Password,
                cancellationToken);

            if (user is null)
            {
                return Results.Json(
                    new ErrorEnvelope(new ApiError(
                        "invalid_credentials",
                        "Invalid username or password.")),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var token = tokenService.Create(user);
            return Results.Ok(new LoginResponse(
                token.AccessToken,
                token.ExpiresAt,
                UserService.ToResponse(user)));
        });

        return endpoints;
    }
}