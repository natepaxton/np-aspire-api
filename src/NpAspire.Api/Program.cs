using NpAspire.Api.Authentication;
using NpAspire.Api.Routing;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery, and HTTP resilience (see NpAspire.ServiceDefaults).
builder.AddServiceDefaults();

builder.Services.AddAuth0Authentication(builder.Configuration);
builder.Services.AddControllers(options =>
    options.Conventions.Add(new RoutePrefixConvention(RoutePrefixConvention.Api)));
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// No UseHttpsRedirection for now: TLS termination is decided with the hosting target (docs/spec.md §7).

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
