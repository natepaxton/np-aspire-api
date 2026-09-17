using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using NpAspire.Api.Authentication;

namespace NpAspire.Api.Tests.Authentication;

public class Auth0OptionsTests
{
    [Theory]
    [InlineData("dev-abc.us.auth0.com")]
    [InlineData("dev-abc.us.auth0.com/")]
    [InlineData("https://dev-abc.us.auth0.com")]
    [InlineData("HTTPS://dev-abc.us.auth0.com/")]
    [InlineData("  dev-abc.us.auth0.com  ")]
    public void Authority_IsNormalizedToHttpsWithTrailingSlash(string domain)
    {
        var options = new Auth0Options { Domain = domain, Audience = "https://api" };

        Assert.Equal("https://dev-abc.us.auth0.com/", options.Authority);
    }

    [Theory]
    [InlineData(null, "https://api")]
    [InlineData("dev-abc.us.auth0.com", null)]
    public void Startup_Fails_WhenAuth0SettingIsMissing(string? domain, string? audience)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            if (domain is not null)
            {
                builder.UseSetting("Auth0:Domain", domain);
            }

            if (audience is not null)
            {
                builder.UseSetting("Auth0:Audience", audience);
            }
        });

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("Auth0", exception.Message);
    }
}
