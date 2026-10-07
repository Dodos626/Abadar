using Abadar.Backend.Data;
using Abadar.Backend.Hubs;
using Abadar.Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Abadar.Backend.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AppUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserResponse?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task<UserResponse?> UpdateProfileAsync(Guid id, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken);
}

public sealed class UserService(
    AbadarDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher,
    IUserEvents userEvents) : IUserService
{
    public async Task<IReadOnlyList<UserResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Username)
            .Select(user => ToResponse(user))
            .ToListAsync(cancellationToken);

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == id,
            cancellationToken);

        return user is null ? null : ToResponse(user);
    }

    public async Task<AppUser?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedLogin = UserNormalization.Normalize(username);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            value => value.NormalizedUsername == normalizedLogin
                || value.NormalizedEmail == normalizedLogin,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        user.LastOnline = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await userEvents.OnUserUpdatedAsync(ToResponse(user), cancellationToken);

        return user;
    }

    public async Task<UserResponse> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCommon(request.FirstName, request.LastName, request.Email, request.Username);
        ValidatePassword(request.Password);
        var role = ParseRole(request.Role);

        var user = new AppUser
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
            NormalizedEmail = UserNormalization.Normalize(request.Email),
            Username = request.Username.Trim(),
            NormalizedUsername = UserNormalization.Normalize(request.Username),
            PasswordHash = string.Empty,
            Role = role
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        dbContext.Users.Add(user);
        await SaveChangesAsync(cancellationToken);

        var response = ToResponse(user);
        await userEvents.OnUserCreatedAsync(response, cancellationToken);
        return response;
    }

    public async Task<UserResponse?> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            value => value.Id == id,
            cancellationToken);
        if (user is null)
        {
            return null;
        }

        ValidateCommon(request.FirstName, request.LastName, request.Email, request.Username);
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = request.Email.Trim();
        user.NormalizedEmail = UserNormalization.Normalize(request.Email);
        user.Username = request.Username.Trim();
        user.NormalizedUsername = UserNormalization.Normalize(request.Username);
        user.Role = ParseRole(request.Role);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            ValidatePassword(request.Password);
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        await SaveChangesAsync(cancellationToken);
        var response = ToResponse(user);
        await userEvents.OnUserUpdatedAsync(response, cancellationToken);
        return response;
    }

    public async Task<UserResponse?> UpdateProfileAsync(
        Guid id,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            value => value.Id == id,
            cancellationToken);
        if (user is null)
        {
            return null;
        }

        ValidateCommon(request.FirstName, request.LastName, request.Email, request.Username);
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = request.Email.Trim();
        user.NormalizedEmail = UserNormalization.Normalize(request.Email);
        user.Username = request.Username.Trim();
        user.NormalizedUsername = UserNormalization.Normalize(request.Username);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            ValidatePassword(request.Password);
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        await SaveChangesAsync(cancellationToken);
        var response = ToResponse(user);
        await userEvents.OnUserUpdatedAsync(response, cancellationToken);
        return response;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (id == currentUserId)
        {
            throw new ApiConflictException("cannot_delete_self", "An administrator cannot delete their own account.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            value => value.Id == id,
            cancellationToken);
        if (user is null)
        {
            return false;
        }

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        await userEvents.OnUserDeletedAsync(id, cancellationToken);
        return true;
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ApiConflictException(
                "user_already_exists",
                "A user with that email or username already exists.");
        }
    }

    private static void ValidateCommon(
        string firstName,
        string lastName,
        string email,
        string username)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 100)
            errors["first_name"] = ["First name is required and must be at most 100 characters."];
        if (string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length > 100)
            errors["last_name"] = ["Last name is required and must be at most 100 characters."];
        if (string.IsNullOrWhiteSpace(email)
            || email.Trim().Length > 320
            || !System.Net.Mail.MailAddress.TryCreate(email.Trim(), out _))
            errors["email"] = ["A valid email address is required."];
        if (string.IsNullOrWhiteSpace(username)
            || username.Trim().Length is < 3 or > 50)
            errors["username"] = ["Username must be between 3 and 50 characters."];

        if (errors.Count > 0)
        {
            throw new ApiValidationException(errors);
        }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ApiValidationException(new Dictionary<string, string[]>
            {
                ["password"] = ["Password must be at least 6 characters."]
            });
        }
    }

    private static UserRole ParseRole(string role)
    {
        if (!Enum.TryParse<UserRole>(role, true, out var parsedRole))
        {
            throw new ApiValidationException(new Dictionary<string, string[]>
            {
                ["role"] = ["Role must be either Admin or User."]
            });
        }

        return parsedRole;
    }

    public static UserResponse ToResponse(AppUser user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.LastOnline,
        user.Email,
        user.Username,
        user.Role.ToString().ToLowerInvariant(),
        user.CreatedAt,
        user.UpdatedAt);
}