namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Helpers for tagging our own operations that depend on another system with the bounded class of a response's
/// status code. The request itself is measured by HttpClient instrumentation (its span and
/// <c>http.client.request.duration</c>), so no span or metric here wraps a single HTTP call.
/// </summary>
public static class ExternalCallTelemetry
{
    /// <summary>
    /// Gets the bounded class of an HTTP status code: <c>1xx</c> to <c>5xx</c>, or <c>none</c> when no response was
    /// received.
    /// </summary>
    /// <param name="statusCode">The status code, or <see langword="null"/>.</param>
    /// <returns>The status class.</returns>
    public static string GetStatusClass(int? statusCode)
    {
        return statusCode switch
        {
            >= 100 and < 200 => "1xx",
            >= 200 and < 300 => "2xx",
            >= 300 and < 400 => "3xx",
            >= 400 and < 500 => "4xx",
            >= 500 and < 600 => "5xx",
            _ => "none",
        };
    }
}
