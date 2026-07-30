using EventTix.Booking.Api.Middleware;
using EventTix.Booking.Application;

var builder = WebApplication.CreateBuilder(args);

// 1. Dependency Injection
builder.Services.AddApplicationServices();

// Registrazione del Global Exception Handler e Problem Details
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// --- 2. Middleware & Endpoints ---
app.UseExceptionHandler();

app.Run();