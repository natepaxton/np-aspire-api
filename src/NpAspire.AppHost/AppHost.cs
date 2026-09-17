var builder = DistributedApplication.CreateBuilder(args);

// Auth0 tenant settings (not secrets). Set them once in the AppHost's user secrets:
//   dotnet user-secrets set "Parameters:auth0-domain" "<tenant>.us.auth0.com" --project src/NpAspire.AppHost
//   dotnet user-secrets set "Parameters:auth0-audience" "<API identifier>" --project src/NpAspire.AppHost
// If they're missing, the Aspire dashboard asks for them.
var auth0Domain = builder.AddParameter("auth0-domain");
var auth0Audience = builder.AddParameter("auth0-audience");

builder.AddProject<Projects.NpAspire_Api>("api")
    .WithEnvironment("Auth0__Domain", auth0Domain)
    .WithEnvironment("Auth0__Audience", auth0Audience)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
