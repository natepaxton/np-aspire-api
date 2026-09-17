var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery, and HTTP resilience (see aspire/ServiceDefaults).
builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No UseHttpsRedirection: TLS terminates at the NGINX gateway.

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
