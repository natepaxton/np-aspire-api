using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NpAspire.Api.Health;

namespace NpAspire.Api.Tests.Health;

/// <summary>
/// Outside Development, health checks are served only on the management port, as in the container.
/// These tests run the API on real Kestrel ports because the port match uses the connection's local port.
/// </summary>
public sealed class ManagementPortTests : IAsyncLifetime
{
    private readonly int _appPort = GetFreePort();
    private readonly int _managementPort = GetFreePort();
    private WebApplicationFactory<Program>? _factory;
    private readonly HttpClient _client = new();

    public ValueTask InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("HealthChecks:ManagementPort", _managementPort.ToString())
            .UseSetting("urls", $"http://127.0.0.1:{_appPort};http://127.0.0.1:{_managementPort}"));
        _factory.UseKestrel();
        _factory.StartServer();
        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task HealthEndpoints_AreServedOnManagementPort(string path)
    {
        var response = await _client.GetAsync($"http://127.0.0.1:{_managementPort}{path}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task HealthEndpoints_AreNotServedOnAppPort(string path)
    {
        var response = await _client.GetAsync($"http://127.0.0.1:{_appPort}{path}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoints_IgnoreSpoofedHostHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_appPort}/health");
        request.Headers.Host = $"127.0.0.1:{_managementPort}";

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthProbe_SucceedsAgainstManagementPort()
    {
        var exitCode = await HealthProbe.RunAsync(new Uri($"http://127.0.0.1:{_managementPort}/health"));

        Assert.Equal(0, exitCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
