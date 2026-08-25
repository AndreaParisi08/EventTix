namespace EventTix.Booking.Infrastructure;

using EventTix.Booking.Application.Abstractions;
using EventTix.Booking.Infrastructure.Persistence;
using EventTix.Booking.Infrastructure.Persistence.Repositories;
using EventTix.BuildingBlocks.Infrastructure.Locking;
using EventTix.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Redis Setup
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string 'Redis' is missing from configuration.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnection));

        services.AddScoped<IDistributedLockService, RedisDistributedLockService>();

        // 2. PostgreSQL & EF Core Setup
        var postgresConnection = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Database connection string 'Database' is missing from configuration.");

        // Registered as a normal DI service (not "new'd" inline below) so it resolves through the
        // container like everything else — the idiomatic EF Core pattern for interceptors, and the
        // only option once/if this interceptor ever needs a constructor dependency (a clock,
        // a logger, ...) of its own. Singleton because it is stateless: it only reads/writes the
        // DbContext instance handed to it per call, it holds nothing itself.
        services.AddSingleton<OutboxSaveChangesInterceptor>();

        services.AddDbContext<BookingDbContext>((serviceProvider, options) =>
            options.UseNpgsql(postgresConnection)
                .AddInterceptors(serviceProvider.GetRequiredService<OutboxSaveChangesInterceptor>()));

        // 3. Persistence Layer (Repositories & Unit of Work)
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork<BookingDbContext>>();

        return services;
    }
}
