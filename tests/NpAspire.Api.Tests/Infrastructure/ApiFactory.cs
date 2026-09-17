using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace NpAspire.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the API in memory with test Auth0 settings. Tokens are signed with a local RSA key that the API trusts in
/// place of the tenant's JWKS, so no request ever reaches Auth0.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Domain = "test-tenant.auth0.local";
    public const string Issuer = $"https://{Domain}/";
    public const string Audience = "https://api.np-aspire.test";

    private static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048)) { KeyId = "test-signing-key" };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Auth0:Domain", Domain);
        builder.UseSetting("Auth0:Audience", Audience);

        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(SigningKey);
                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            }));
    }

    /// <summary>Creates an access token; by default a valid one for this API.</summary>
    public static string CreateToken(
        string issuer = Issuer,
        string audience = Audience,
        DateTime? expires = null,
        SigningCredentials? signingCredentials = null,
        string subject = "auth0|test-user")
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([new Claim("sub", subject)]),
            IssuedAt = expiresAt.AddHours(-1),
            NotBefore = expiresAt.AddHours(-1),
            Expires = expiresAt,
            SigningCredentials = signingCredentials ?? new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }
}
