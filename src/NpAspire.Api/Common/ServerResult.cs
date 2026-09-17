namespace NpAspire.Api.Common;

/// <summary>
/// Envelope for data returned by a server call, with the messages collected while the request was handled.
/// </summary>
/// <typeparam name="T">Type of the returned data.</typeparam>
public sealed class ServerResult<T>
{
    /// <summary>The data returned by the call, if any.</summary>
    public T? Data { get; set; }

    /// <summary>HTTP status code of the call.</summary>
    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    /// <summary>Messages of errors that occurred while handling the request.</summary>
    public List<string> ErrorMessages { get; } = [];

    /// <summary>Warnings collected while handling the request.</summary>
    public List<string> WarningMessages { get; } = [];

    /// <summary>Success messages collected while handling the request.</summary>
    public List<string> SuccessMessages { get; } = [];

    /// <summary>Records an error by its <see cref="Exception.Message"/>.</summary>
    public void AddError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ErrorMessages.Add(exception.Message);
    }

    public bool HasErrors() => ErrorMessages.Count > 0;

    public bool HasWarnings() => WarningMessages.Count > 0;

    public bool HasSuccesses() => SuccessMessages.Count > 0;

    /// <summary>True when <see cref="StatusCode"/> is in the 2xx range.</summary>
    public bool IsSuccessful() => StatusCode is >= 200 and <= 299;
}
