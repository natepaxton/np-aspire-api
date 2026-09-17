namespace NpAspire.Api.Health;

/// <summary>
/// Minimal HTTP health probe for container health checks. The chiseled runtime image has no shell or curl,
/// so the container runs <c>dotnet NpAspire.Api.dll --health-check &lt;url&gt;</c> instead.
/// </summary>
public static class HealthProbe
{
    public const string Argument = "--health-check";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Returns 0 when <paramref name="url"/> answers with a success status code, otherwise 1.</summary>
    public static async Task<int> RunAsync(Uri url, HttpMessageHandler? handler = null)
    {
        using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = Timeout;

        try
        {
            using var response = await client.GetAsync(url);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
