using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Abadar.Backend.Data;
using Abadar.Backend.Endpoints;
using Abadar.Backend.Hubs;
using Abadar.Backend.Models;
using Abadar.Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "abadar-backend";
const string CorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

var allowedOrigin = builder.Configuration["CORS_ALLOWED_ORIGIN"]
    ?? "http://localhost:3000";

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigin)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_CONNECTION_STRING"]
    ?? "Host=localhost;Port=5432;Database=abadar;Username=abadar;Password=abadar_dev";

builder.Services.AddDbContext<AbadarDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(2), null)));

var jwtOptions = JwtOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/users"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
        options.UseSecurityTokenValidators = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

builder.Services.AddSingleton(new ApplicationMetadata(
    ServiceName,
    builder.Configuration["APP_VERSION"] ?? "dev",
    DateTimeOffset.UtcNow));
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
builder.Services.AddScoped<IUserEvents, SignalRUserEvents>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(5);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
});

var port = builder.Configuration["PORT"] ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

app.Use(async (context, next) =>
{
    var suppliedRequestId = context.Request.Headers["X-Request-ID"].FirstOrDefault();
    var requestId = string.IsNullOrWhiteSpace(suppliedRequestId)
        ? RandomNumberGenerator.GetHexString(12).ToLowerInvariant()
        : suppliedRequestId.Trim();

    context.TraceIdentifier = requestId;
    context.Response.Headers["X-Request-ID"] = requestId;
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";

    var stopwatch = Stopwatch.StartNew();

    try
    {
        await next(context);
    }
    finally
    {
        stopwatch.Stop();
        app.Logger.LogInformation(
            "Request completed {RequestId} {Method} {Path} {StatusCode} in {DurationMs} ms",
            requestId,
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds);
    }
});

app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    await next(context);

    if (context.Response.StatusCode != StatusCodes.Status404NotFound
        || context.Response.HasStarted)
    {
        return;
    }

    await context.Response.WriteAsJsonAsync(new ErrorEnvelope(new ApiError(
        "route_not_found",
        "The requested API route does not exist.",
        context.TraceIdentifier)));
});

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapSystemEndpoints();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapHub<UsersHub>("/hubs/users")
    .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.Run();

public partial class Program;