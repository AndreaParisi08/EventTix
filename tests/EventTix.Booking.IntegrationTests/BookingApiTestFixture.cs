using EventTix.Booking.Application;
using EventTix.Booking.Infrastructure;
using EventTix.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace EventTix.Booking.IntegrationTests;

/// <summary>
/// Spins up REAL, ephemeral Postgres and Redis containers (Testcontainers) and composes the actual
/// Application + Infrastructure DI graph against them — the same wiring Program.cs uses, minus the
/// HTTP layer. Deliberately no mocks for Redis/Postgres: the property under test — "only one of many
/// concurrent requests for the same seat wins" — depends on the REAL behavior of Redis's atomic lock
/// and Postgres' own consistency, not on how a mock happens to be scripted. A mocked lock service
/// would make the test pass by construction and prove nothing about actual concurrency safety.
///
/// Shared across every [Fact] in a test class via IClassFixture: starting two containers costs a
/// few seconds, paid once per test run rather than once per test.
/// </summary>
public sealed class BookingApiTestFixture : IAsyncLifetime
{
    // Image passed to the constructor, not via .WithImage(...): recent Testcontainers releases
    // obsoleted the parameterless builder constructors specifically so nobody accidentally ends up
    // on an unpinned/outdated default image — the image is now a required, explicit choice.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine") // same tag as docker-compose.yml, for parity with dev/prod.
        .WithDatabase("eventtix_db")
        .WithUsername("eventtix")
        .WithPassword("eventtix_dev_password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    private ServiceProvider? _serviceProvider;

    public IServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException(
            "BookingApiTestFixture not initialized yet — InitializeAsync must run first (xUnit does this automatically via IAsyncLifetime).");

    public async Task InitializeAsync()
    {
        // No dependency between the two containers — start them concurrently rather than one after
        // the other, it roughly halves the fixed startup cost.
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _postgres.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
            })
            .Build();

        var services = new ServiceCollection();

        // WebApplication.CreateBuilder(args) registers this for free in the real app; building a
        // bare ServiceCollection here does not. MediatR (this version) requires an ILoggerFactory
        // to be present at registration time, even with no logging provider actually configured.
        services.AddLogging();

        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);
        _serviceProvider = services.BuildServiceProvider();

        // Same "apply migrations on startup" step Program.cs runs in Development — here it creates
        // the schema (bookings, outbox_messages) on the fresh, empty container before any test runs.
        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}
