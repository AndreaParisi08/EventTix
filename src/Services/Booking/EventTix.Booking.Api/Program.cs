using EventTix.Booking.Api.Endpoints;
using EventTix.Booking.Application;
using EventTix.Booking.Infrastructure;
using EventTix.Booking.Infrastructure.Persistence;
using EventTix.BuildingBlocks.Domain.Exceptions;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Layer Dependencies
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Configure Exception Handling & Problem Details (RFC 7807)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// 3. Configure Infrastructure Health Checks (Liveness & Readiness)
var postgresConnection = builder.Configuration.GetConnectionString("Database")!;
var redisConnection = builder.Configuration.GetConnectionString("Redis")!;

builder.Services.AddHealthChecks()
    .AddNpgSql(postgresConnection, name: "postgres", tags: new[] { "ready" })
    .AddRedis(redisConnection, name: "redis", tags: new[] { "ready" });

// 4. Configure OpenAPI / Swagger Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 5. Middleware Pipeline
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 6. Map Health Check Endpoints for Kubernetes / Orchestrators
// Liveness probe: checks if the container HTTP server is responsive
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness probe: checks if PostgreSQL and Redis dependencies are operational
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// 7. Map Domain Minimal API Endpoints
app.MapBookingEndpoints();

// 8. Auto-apply EF Core Migrations on startup in Development environment
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();