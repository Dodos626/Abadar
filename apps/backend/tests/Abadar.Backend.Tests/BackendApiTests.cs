using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Abadar.Backend.Data;
using Abadar.Backend.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

public sealed class BackendApiTests : IClassFixture<BackendWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _client;

    public BackendApiTests(BackendWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Readiness_ReturnsHealthyResponseAndRequestId()
    {
        using var response = await _client.GetAsync("/api/v1/health/ready");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
        Assert.Equal("up", body.Checks?["http_server"]);
        Assert.True(response.Headers.Contains("X-Request-ID"));
    }

    [Fact]
    public async Task Markets_ReturnsSimulatedMarkets()
    {
        using var response = await _client.GetAsync("/api/v1/markets");
        var body = await response.Content.ReadFromJsonAsync<MarketsResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(3, body.Data.Count);
        Assert.Equal("BTC/USD", body.Data[0].Symbol);
    }

    [Fact]
    public async Task UnsupportedMethod_ReturnsMethodNotAllowed()
    {
        using var response = await _client.PostAsync("/api/v1/markets", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsStructuredNotFoundResponse()
    {
        using var response = await _client.GetAsync("/does-not-exist");
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("route_not_found", body?.Error.Code);
    }

    [Fact]
    public async Task CorsPreflight_AllowsConfiguredFrontendOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/markets");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "http://localhost:3000",
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task AdminLogin_CanCreateAndDeleteUser()
    {
        var adminLogin = await LoginAsync("admin", "admin123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            adminLogin.AccessToken);

        using var createResponse = await _client.PostAsync(
            "/api/v1/users",
            JsonContent.Create(
                new CreateUserRequest(
                    "Ada",
                    "Lovelace",
                    "ada@example.test",
                    "ada",
                    "secure123",
                    "user"),
                options: JsonOptions));
        if (!createResponse.IsSuccessStatusCode)
        {
            throw new Xunit.Sdk.XunitException(
                $"Create failed with {(int)createResponse.StatusCode}: {await createResponse.Content.ReadAsStringAsync()}");
        }
        var created = await createResponse.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);

        using var deleteResponse = await _client.DeleteAsync($"/api/v1/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CannotListAllUsers()
    {
        var userLogin = await LoginAsync("user", "user123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            userLogin.AccessToken);

        using var response = await _client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CannotNegotiateAdminUsersHub()
    {
        var userLogin = await LoginAsync("user", "user123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            userLogin.AccessToken);

        using var response = await _client.PostAsync(
            "/hubs/users/negotiate?negotiateVersion=1",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<LoginResponse> LoginAsync(string username, string password)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(username, password));

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"Login for {username} failed with {(int)response.StatusCode}: {body}");
        }
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions))!;
    }
}

public sealed class BackendWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"abadar-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var dbContextDescriptors = services
                .Where(descriptor => descriptor.ServiceType == typeof(AbadarDbContext)
                    || descriptor.ServiceType == typeof(DbContextOptions<AbadarDbContext>)
                    || descriptor.ServiceType == typeof(IDbContextOptionsConfiguration<AbadarDbContext>))
                .ToList();

            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AbadarDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}