using System.Net;
using System.Net.Http.Headers;
using NpAspire.Api.Tests.Infrastructure;

namespace NpAspire.Api.Tests.Endpoints;

public class DefaultEndpointsTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task HealthEndpoints_ReturnHealthy(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OpenApiDocument_IsServedInDevelopment()
    {
        var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsUnauthorized_WhenAnonymous()
    {
        // Deny by default: anonymous callers can't probe which routes exist.
        var response = await _client.GetAsync("/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFound_WhenAuthenticated()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/does-not-exist");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.CreateToken());

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
