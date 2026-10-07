using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Abadar.Backend.Models;
using Microsoft.IdentityModel.Tokens;

namespace Abadar.Backend.Services;

public interface ITokenService
{
    TokenResult Create(AppUser user);
}

public sealed class TokenService(JwtOptions options) : ITokenService
{
    private readonly SymmetricSecurityKey _signingKey = new(
        Encoding.UTF8.GetBytes(options.SigningKey));

    public TokenResult Create(AppUser user)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(options.Lifetime);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                _signingKey,
                SecurityAlgorithms.HmacSha256));

        return new TokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}

public sealed record TokenResult(string AccessToken, DateTimeOffset ExpiresAt);