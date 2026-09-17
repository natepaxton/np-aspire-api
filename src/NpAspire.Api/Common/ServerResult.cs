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

    /// <summary>Stack traces of errors that occurred while handling the request (Development only).</summary>
    public List<string> StackTrace { get; } = [];

    /// <summary>
    /// Records an error by its <see cref="Exception.Message"/> and, when <paramref name="includeStackTrace"/> is set
    /// and the exception was thrown, its <see cref="Exception.StackTrace"/>.
    /// </summary>
    /// <param name="exception">The error to record.</param>
    /// <param name="includeStackTrace">
    /// Whether to record the stack trace. Pass <c>true</c> only in Development: stack traces reveal code structure.
    /// </param>
    public void AddError(Exception exception, bool includeStackTrace = false)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ErrorMessages.Add(exception.Message);

        // An exception that was created but never thrown has no stack trace.
        if (includeStackTrace && exception.StackTrace is not null)
        {
            StackTrace.Add(exception.StackTrace);
        }
    }

    public bool HasErrors() => ErrorMessages.Count > 0;

    public bool HasWarnings() => WarningMessages.Count > 0;

    public bool HasSuccesses() => SuccessMessages.Count > 0;

    /// <summary>True when <see cref="StatusCode"/> is in the 2xx range.</summary>
    public bool IsSuccessful() => StatusCode is >= 200 and <= 299;
}
