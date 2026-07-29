var builder = WebApplication.CreateBuilder(args);

// --- 1. Registrazione Servizi (Dependency Injection) ---


var app = builder.Build();

// --- 2. Middleware & Endpoints ---


app.Run();