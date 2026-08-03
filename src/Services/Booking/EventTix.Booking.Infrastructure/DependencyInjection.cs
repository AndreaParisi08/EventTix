namespace EventTix.Booking.Infrastructure;

using EventTix.Booking.Application.Abstractions;
using EventTix.Booking.Infrastructure.Locking;
using EventTix.Booking.Infrastructure.Persistence;
using EventTix.Booking.Infrastructure.Persistence.Repositories;
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

        services.AddDbContext<BookingDbContext>(options =>
            options.UseNpgsql(postgresConnection));

        // 3. Persistence Layer (Repositories & Unit of Work)
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}