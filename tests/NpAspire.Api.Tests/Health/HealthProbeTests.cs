using System.Net;
using NpAspire.Api.Health;

namespace NpAspire.Api.Tests.Health;

public class HealthProbeTests
{
    private static readonly Uri Url = new("http://localhost:8081/health");

    [Fact]
    public async Task ReturnsZero_WhenEndpointIsHealthy()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        Assert.Equal(0, await HealthProbe.RunAsync(Url, handler));
    }

    [Fact]
    public async Task ReturnsOne_WhenEndpointIsUnhealthy()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        Assert.Equal(1, await HealthProbe.RunAsync(Url, handler));
    }

    [Fact]
    public async Task ReturnsOne_WhenEndpointIsUnreachable()
    {
        using var handler = new StubHandler(_ => throw new HttpRequestException("Connection refused"));

        Assert.Equal(1, await HealthProbe.RunAsync(Url, handler));
    }

    [Fact]
    public async Task ReturnsOne_WhenRequestTimesOut()
    {
        using var handler = new StubHandler(_ => throw new TaskCanceledException("Timed out"));

        Assert.Equal(1, await HealthProbe.RunAsync(Url, handler));
    }

    [Fact]
    public void EntryPoint_ReturnsOne_WhenEndpointIsUnreachable()
    {
        // Nothing listens on port 1; the probe mode must exit without starting the web host.
        string[] args = [HealthProbe.Argument, "http://127.0.0.1:1/health"];
        var entryPoint = typeof(Program).Assembly.EntryPoint!;

        var exitCode = entryPoint.Invoke(null, [args]);

        Assert.Equal(1, exitCode);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
