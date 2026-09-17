using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NpAspire.Api.Authorization;

namespace NpAspire.Api.Authentication;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Validates Auth0 access tokens (signature via the tenant's JWKS, issuer, audience, lifetime) and makes every
    /// endpoint require an authenticated user unless it opts out with <c>AllowAnonymous</c>.
    /// </summary>
    public static IServiceCollection AddAuth0Authentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<Auth0Options>()
            .Bind(configuration.GetSection(Auth0Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<Auth0Options>>((jwt, auth0) =>
            {
                jwt.Authority = auth0.Value.Authority;
                jwt.Audience = auth0.Value.Audience;
                // Keep Auth0's claim names ("sub", "permissions") instead of mapping them to legacy URIs.
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.NameClaimType = "sub";
                // Auth0 signs access tokens for APIs with RS256.
                jwt.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
            });

        // Deny by default, then one policy per permission (Permissions.g.cs, from infra/auth0/permissions.json).
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPermissionPolicies();

        return services;
    }
}
