using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NpAspire.Api.Tests.Infrastructure;

namespace NpAspire.Api.Tests.Controllers;

public class DiagnosticsControllerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string StatusPath = "/api/v1/diagnostics";

    [Fact]
    public async Task CheckStatus_ReturnsHealthy_WithoutToken()
    {
        var response = await factory.CreateClient().GetAsync(StatusPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadBodyAsync(response);
        var result = body.RootElement;
        Assert.Equal((int)HealthStatus.Healthy, result.GetProperty("data").GetInt32());
        Assert.Equal(200, result.GetProperty("statusCode").GetInt32());
        Assert.Equal(0, result.GetProperty("errorMessages").GetArrayLength());
        Assert.Equal(0, result.GetProperty("warningMessages").GetArrayLength());
        Assert.Equal(0, result.GetProperty("stackTrace").GetArrayLength());
    }

    [Fact]
    public async Task CheckStatus_ReturnsOk_WithValidToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, StatusPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.CreateToken());

        var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CheckStatus_IsOnlyServedUnderTheApiPrefix()
    {
        // Without the prefix the route doesn't exist, so an anonymous caller gets the deny-by-default 401.
        var response = await factory.CreateClient().GetAsync("/diagnostics", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CheckStatus_ReturnsOkWithWarning_WhenACheckIsDegraded()
    {
        var client = CreateClient(services =>
            services.AddHealthChecks().AddCheck("degraded", () => HealthCheckResult.Degraded("slow")));

        var response = await client.GetAsync(StatusPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadBodyAsync(response);
        var result = body.RootElement;
        Assert.Equal((int)HealthStatus.Degraded, result.GetProperty("data").GetInt32());
        Assert.Equal(1, result.GetProperty("warningMessages").GetArrayLength());
        Assert.Equal(0, result.GetProperty("errorMessages").GetArrayLength());
    }

    [Fact]
    public async Task CheckStatus_ReturnsServiceUnavailable_WhenACheckIsUnhealthy()
    {
        var client = CreateClient(services =>
            services.AddHealthChecks().AddCheck("failing-check", () => HealthCheckResult.Unhealthy("secret detail")));

        var response = await client.GetAsync(StatusPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(json);
        var result = body.RootElement;
        Assert.Equal((int)HealthStatus.Unhealthy, result.GetProperty("data").GetInt32());
        Assert.Equal(503, result.GetProperty("statusCode").GetInt32());
        Assert.Equal(1, result.GetProperty("errorMessages").GetArrayLength());
        // Anonymous callers only see the overall status, not which check failed or why.
        Assert.DoesNotContain("failing-check", json);
        Assert.DoesNotContain("secret detail", json);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public async Task CheckStatus_ReturnsServerError_WhenHealthChecksThrow(string environment, bool expectStackTrace)
    {
        var client = CreateClient(
            services => services.AddSingleton<HealthCheckService, ThrowingHealthCheckService>(),
            environment);

        var response = await client.GetAsync(StatusPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var body = await ReadBodyAsync(response);
        var result = body.RootElement;
        Assert.Equal(500, result.GetProperty("statusCode").GetInt32());
        Assert.Equal(ThrowingHealthCheckService.Message, result.GetProperty("errorMessages")[0].GetString());
        Assert.Equal(expectStackTrace ? 1 : 0, result.GetProperty("stackTrace").GetArrayLength());
    }

    private HttpClient CreateClient(Action<IServiceCollection> configureServices, string environment = "Development") =>
        factory
            .WithWebHostBuilder(builder => builder
                .UseEnvironment(environment)
                .ConfigureTestServices(configureServices))
            .CreateClient();

    private static async Task<JsonDocument> ReadBodyAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    /// <summary>Stands in for a health check service that can't run at all, to exercise the 500 path.</summary>
    private sealed class ThrowingHealthCheckService : HealthCheckService
    {
        public const string Message = "Health checks could not run.";

        public override Task<HealthReport> CheckHealthAsync(
            Func<HealthCheckRegistration, bool>? predicate,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Message);
    }
}
