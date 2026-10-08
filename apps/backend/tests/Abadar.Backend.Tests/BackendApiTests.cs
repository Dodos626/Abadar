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
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Abadar.MatchingEngine;
using Xunit;

// Exercises operational, authentication, administration, persistence, and recovery API flows.
public sealed class BackendApiTests
{
    // Matches the backend's snake_case JSON contract during test serialization.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // Sends requests through an in-memory ASP.NET Core test server.
    private readonly HttpClient _client;

    // Creates an isolated application and database for each test instance.
    public BackendApiTests()
    {
        _client = new BackendWebApplicationFactory().CreateClient();
    }

    [Fact]
    // Verifies readiness reports database connectivity and request correlation.
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
    // Verifies the public market snapshot remains available.
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
    // Verifies unsupported verbs return the framework method-not-allowed response.
    public async Task UnsupportedMethod_ReturnsMethodNotAllowed()
    {
        using var response = await _client.PostAsync("/api/v1/markets", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    // Verifies unknown routes use the structured API error envelope.
    public async Task UnknownRoute_ReturnsStructuredNotFoundResponse()
    {
        using var response = await _client.GetAsync("/does-not-exist");
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>(JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("route_not_found", body?.Error.Code);
    }

    [Fact]
    // Verifies CORS preflight permits the configured frontend origin.
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
    // Verifies an administrator can create and remove a user through JWT authorization.
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
    // Verifies a standard user cannot access the administrator user collection.
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
    // Verifies a standard user cannot negotiate the administrator SignalR hub.
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

    [Fact]
    // Verifies simulations persist queryable order and trade history.
    public async Task Admin_CanSimulateMarketAndReadDurableHistory()
    {
        var adminLogin = await LoginAsync("admin", "admin123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            adminLogin.AccessToken);

        using var simulationResponse = await _client.PostAsync(
            "/api/v1/admin/simulations",
            JsonContent.Create(
                new SimulateMarketRequest(
                    ["BTC/USD"],
                    20,
                    20,
                    99,
                    101,
                    1,
                    2,
                    50,
                    20,
                    42),
                options: JsonOptions));
        var simulation = await simulationResponse.Content
            .ReadFromJsonAsync<SimulateMarketResponse>(JsonOptions);

        if (!simulationResponse.IsSuccessStatusCode)
        {
            throw new Xunit.Sdk.XunitException(
                $"Simulation failed with {(int)simulationResponse.StatusCode}: {await simulationResponse.Content.ReadAsStringAsync()}");
        }
        Assert.Equal(HttpStatusCode.OK, simulationResponse.StatusCode);
        Assert.NotNull(simulation);
        Assert.Equal(20, simulation.OrdersSubmitted);

        var orders = await _client.GetFromJsonAsync<List<OrderHistoryResponse>>(
            "/api/v1/orders?symbol=BTC%2FUSD&limit=100",
            JsonOptions);
        var trades = await _client.GetFromJsonAsync<List<TradeHistoryResponse>>(
            "/api/v1/trades?symbol=BTC%2FUSD&limit=100",
            JsonOptions);

        Assert.NotNull(orders);
        Assert.Equal(20, orders.Count);
        Assert.NotNull(trades);
        Assert.Equal(simulation.TradesExecuted, trades.Count);
    }

    [Fact]
    // Verifies a multi-batch simulation persists every generated order.
    public async Task Admin_CanRunBatchedSimulation()
    {
        var adminLogin = await LoginAsync("admin", "admin123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            adminLogin.AccessToken);

        using var response = await _client.PostAsync(
            "/api/v1/admin/simulations",
            JsonContent.Create(
                new SimulateMarketRequest(
                    ["BTC/USD"],
                    600,
                    600,
                    95,
                    105,
                    0.1m,
                    2,
                    50,
                    15,
                    42),
                options: JsonOptions));
        var simulation = await response.Content.ReadFromJsonAsync<SimulateMarketResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(simulation);
        Assert.Equal(600, simulation.OrdersSubmitted);

        var orders = await _client.GetFromJsonAsync<List<OrderHistoryResponse>>(
            "/api/v1/orders?symbol=BTC%2FUSD&limit=500",
            JsonOptions);
        Assert.NotNull(orders);
        Assert.Equal(500, orders.Count);
    }

    [Fact]
    // Verifies reset removes all data except the calling administrator account.
    public async Task DatabaseReset_PreservesCallingAdminOnly()
    {
        var adminLogin = await LoginAsync("admin", "admin123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            adminLogin.AccessToken);

        using var response = await _client.PostAsync("/api/v1/admin/database/reset", null);
        var result = await response.Content.ReadFromJsonAsync<ResetDatabaseResponse>(JsonOptions);
        var users = await _client.GetFromJsonAsync<List<UserResponse>>("/api/v1/users", JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(adminLogin.User.Id, result.PreservedAdminId);
        Assert.NotNull(users);
        Assert.Single(users);
        Assert.Equal(adminLogin.User.Id, users[0].Id);
    }

    [Fact]
    // Verifies a new backend instance rebuilds open books from durable state.
    public async Task DurableOpenOrders_RecoverAfterBackendRestart()
    {
        var databaseName = $"abadar-recovery-{Guid.NewGuid()}";
        var databaseRoot = new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot();

        using (var firstFactory = new BackendWebApplicationFactory(databaseName, databaseRoot))
        using (var firstClient = firstFactory.CreateClient())
        {
            var adminLogin = await LoginAsync(firstClient, "admin", "admin123");
            firstClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                adminLogin.AccessToken);
            using var simulation = await firstClient.PostAsync(
                "/api/v1/admin/simulations",
                JsonContent.Create(
                    new SimulateMarketRequest(
                        ["BTC/USD"],
                        1,
                        1,
                        100,
                        100,
                        2,
                        2,
                        0,
                        0,
                        7),
                    options: JsonOptions));
            Assert.Equal(HttpStatusCode.OK, simulation.StatusCode);
        }

        using (var secondFactory = new BackendWebApplicationFactory(databaseName, databaseRoot, recoverExchange: true))
        using (var secondClient = secondFactory.CreateClient())
        {
            var adminLogin = await LoginAsync(secondClient, "admin", "admin123");
            secondClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                adminLogin.AccessToken);
            using var simulation = await secondClient.PostAsync(
                "/api/v1/admin/simulations",
                JsonContent.Create(
                    new SimulateMarketRequest(
                        ["BTC/USD"],
                        1,
                        1,
                        100,
                        100,
                        2,
                        2,
                        100,
                        0,
                        8),
                    options: JsonOptions));
            var result = await simulation.Content.ReadFromJsonAsync<SimulateMarketResponse>(JsonOptions);

            Assert.Equal(HttpStatusCode.OK, simulation.StatusCode);
            Assert.NotNull(result);
            Assert.Equal(1, result.TradesExecuted);

            var orders = await secondClient.GetFromJsonAsync<List<OrderHistoryResponse>>(
                "/api/v1/orders?symbol=BTC%2FUSD&limit=10",
                JsonOptions);
            Assert.NotNull(orders);
            Assert.Equal([2L, 1L], orders.Select(order => order.Sequence));
            Assert.All(orders, order => Assert.Equal("filled", order.Status));
        }
    }

    // Logs into the default test client and returns its token contract.
    private async Task<LoginResponse> LoginAsync(string username, string password)
        => await LoginAsync(_client, username, password);

    // Logs into a supplied restart-test client and fails with the response body on error.
    private static async Task<LoginResponse> LoginAsync(
        HttpClient client,
        string username,
        string password)
    {
        using var response = await client.PostAsJsonAsync(
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

// Replaces PostgreSQL and the singleton engine with isolated test instances.
public sealed class BackendWebApplicationFactory : WebApplicationFactory<Program>
{
    // Names the in-memory database used by this application instance.
    private readonly string _databaseName;
    // Allows restart tests to share one durable in-memory database root.
    private readonly Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot _databaseRoot;
    // Enables explicit recovery when constructing the restarted application.
    private readonly bool _recoverExchange;

    // Creates a fully isolated test application by default.
    public BackendWebApplicationFactory()
        : this(
            $"abadar-tests-{Guid.NewGuid()}",
            new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot(),
            false)
    {
    }

    // Creates a test application that can share state and optionally run recovery.
    public BackendWebApplicationFactory(
        string databaseName,
        Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot databaseRoot,
        bool recoverExchange = false)
    {
        _databaseName = databaseName;
        _databaseRoot = databaseRoot;
        _recoverExchange = recoverExchange;
    }

    // Replaces production infrastructure with deterministic in-memory services.
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
                options.UseInMemoryDatabase(_databaseName, _databaseRoot));
            services.RemoveAll<IMatchingEngine>();
            services.AddSingleton<IMatchingEngine, MatchingEngine>();
            if (_recoverExchange)
            {
                services.RemoveAll<IDatabaseInitializer>();
                services.AddScoped<IDatabaseInitializer, PreserveDatabaseInitializer>();
                services.AddHostedService<ExchangeRecoveryHostedService>();
            }
        });
    }
}

// Runs exchange recovery after test database replacement is complete.
public sealed class ExchangeRecoveryHostedService(IServiceProvider services) : IHostedService
{
    // Creates a scope and restores durable exchange state before requests start.
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var exchangeService = scope.ServiceProvider.GetRequiredService<Abadar.Backend.Services.IExchangeService>();
        await exchangeService.RecoverAsync(cancellationToken);
    }

    // Requires no shutdown work because the matching engine is container-managed.
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

// Preserves the shared in-memory database during restart recovery tests.
public sealed class PreserveDatabaseInitializer : IDatabaseInitializer
{
    // Skips destructive testing initialization for the restarted application.
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}