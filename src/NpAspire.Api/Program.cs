using NpAspire.Api.Health;

// Container health check mode: probe the URL and exit without starting the web host.
if (args is [HealthProbe.Argument, var healthUrl])
{
    return await HealthProbe.RunAsync(new Uri(healthUrl));
}

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery, and HTTP resilience (see NpAspire.ServiceDefaults).
builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No UseHttpsRedirection: TLS terminates at the NGINX gateway. Forwarded headers from the gateway are
// enabled in containers with ASPNETCORE_FORWARDEDHEADERS_ENABLED (see compose.yaml).

app.MapDefaultEndpoints();
app.MapControllers();

await app.RunAsync();
return 0;
