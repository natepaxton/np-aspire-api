using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using NpAspire.Api.Authorization;
using NpAspire.Api.Tests.Infrastructure;

namespace NpAspire.Api.Tests.Controllers;

public class AuthControllerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string CheckPath = "/api/v1/auth/check";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Check_ReturnsOk_WhenTheTokenGrantsReadProfile()
    {
        var response = await SendAsync(ApiFactory.CreateToken(permissions: Permissions.ReadProfile));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Check_ReturnsForbidden_WhenTheTokenGrantsNoPermissions()
    {
        // A user with no Auth0 role: authenticated, but not allowed (docs/spec.md §5).
        var response = await SendAsync(ApiFactory.CreateToken());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Check_ReturnsForbidden_WhenTheTokenGrantsADifferentPermission()
    {
        var response = await SendAsync(ApiFactory.CreateToken(permissions: Permissions.ReadUsers));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Check_ReturnsUnauthorized_WithoutToken()
    {
        var response = await SendAsync(token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Fact]
    public async Task Check_ReturnsUnauthorized_WithExpiredToken()
    {
        var response = await SendAsync(ApiFactory.CreateToken(expires: DateTime.UtcNow.AddMinutes(-10)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Check_ReturnsUnauthorized_WithWrongAudience()
    {
        var response = await SendAsync(ApiFactory.CreateToken(audience: "https://some-other-api"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Check_ReturnsUnauthorized_WithWrongIssuer()
    {
        var response = await SendAsync(ApiFactory.CreateToken(issuer: "https://attacker.example/"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Check_ReturnsUnauthorized_WithUntrustedSigningKey()
    {
        using var rsa = RSA.Create(2048);
        var untrusted = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "test-signing-key" }, SecurityAlgorithms.RsaSha256);

        var response = await SendAsync(ApiFactory.CreateToken(signingCredentials: untrusted));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Check_ReturnsUnauthorized_WithSymmetricallySignedToken()
    {
        // Only RS256 is accepted, so an HS256 token is rejected even before its key is considered.
        var hmac = new SigningCredentials(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64)), SecurityAlgorithms.HmacSha256);

        var response = await SendAsync(ApiFactory.CreateToken(signingCredentials: hmac));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Check_ReturnsUnauthorized_WithMalformedToken()
    {
        var response = await SendAsync("not-a-jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CheckPath);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
