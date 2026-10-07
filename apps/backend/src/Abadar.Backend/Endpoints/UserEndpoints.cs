using System.Security.Claims;
using Abadar.Backend.Models;
using Abadar.Backend.Services;

namespace Abadar.Backend.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/users").RequireAuthorization();

        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var id = GetUserId(principal);
            var user = await userService.GetByIdAsync(id, cancellationToken);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        group.MapPut("/me", async (
            UpdateProfileRequest request,
            ClaimsPrincipal principal,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var user = await userService.UpdateProfileAsync(
                GetUserId(principal),
                request,
                cancellationToken);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        group.MapGet("/", async (
            IUserService userService,
            CancellationToken cancellationToken) =>
            Results.Ok(await userService.GetAllAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        group.MapGet("/{id:guid}", async (
            Guid id,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var user = await userService.GetByIdAsync(id, cancellationToken);
            return user is null ? Results.NotFound() : Results.Ok(user);
        }).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        group.MapPost("/", async (
            CreateUserRequest request,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var user = await userService.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/v1/users/{user.Id}", user);
        }).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateUserRequest request,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var user = await userService.UpdateAsync(id, request, cancellationToken);
            return user is null ? Results.NotFound() : Results.Ok(user);
        }).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await userService.DeleteAsync(
                id,
                GetUserId(principal),
                cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

        return endpoints;
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The authenticated user ID is invalid.");
    }
}