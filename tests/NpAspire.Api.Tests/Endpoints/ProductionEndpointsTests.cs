using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using NpAspire.Api.Tests.Infrastructure;

namespace NpAspire.Api.Tests.Endpoints;

/// <summary>Health checks and the OpenAPI document must not be exposed outside Development.</summary>
public class ProductionEndpointsTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory
        .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
        .CreateClient();

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    [InlineData("/openapi/v1.json")]
    public async Task DevelopmentOnlyEndpoints_AreNotMapped(string path)
    {
        // Authenticated, so a 404 means the endpoint doesn't exist (anonymous callers get 401 for any unknown path).
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.CreateToken());

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/openapi/v1.json")]
    public async Task DevelopmentOnlyEndpoints_RequireAuthentication_WhenAnonymous(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
