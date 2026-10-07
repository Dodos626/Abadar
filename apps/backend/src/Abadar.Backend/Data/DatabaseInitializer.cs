using Abadar.Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Abadar.Backend.Data;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class DatabaseInitializer(
    AbadarDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher,
    IWebHostEnvironment environment,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (environment.IsEnvironment("Testing"))
        {
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        else if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        await SeedUserAsync(
            "System",
            "Administrator",
            "admin@abadar.local",
            "admin",
            "admin123",
            UserRole.Admin,
            cancellationToken);

        await SeedUserAsync(
            "Demo",
            "User",
            "user@abadar.local",
            "user",
            "user123",
            UserRole.User,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedUserAsync(
        string firstName,
        string lastName,
        string email,
        string username,
        string password,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = UserNormalization.Normalize(username);
        if (await dbContext.Users.AnyAsync(
                user => user.NormalizedUsername == normalizedUsername,
                cancellationToken))
        {
            return;
        }

        var user = new AppUser
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            NormalizedEmail = UserNormalization.Normalize(email),
            Username = username,
            NormalizedUsername = normalizedUsername,
            PasswordHash = string.Empty,
            Role = role
        };

        user.PasswordHash = passwordHasher.HashPassword(user, password);
        dbContext.Users.Add(user);
        logger.LogInformation("Seeded development user {Username} with role {Role}", username, role);
    }
}