using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Measures calls to another system, such as a telephony provider's REST API, a payment gateway or a data connector:
/// a client span, the area's duration histogram and how the call ended, tagged with the bounded
/// <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/>, <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/>
/// and <see cref="CrestAppsOrchardCoreTelemetry.StatusClassTag"/>. A call never carries its URL, host or record ids
/// on a metric.
/// </summary>
public static class ExternalCallTelemetry
{
    /// <summary>
    /// Starts measuring a call to another system.
    /// </summary>
    /// <param name="source">The activity source of the area.</param>
    /// <param name="spanName">The name of the span, such as <c>crestapps.telnyx.api.request</c>.</param>
    /// <param name="duration">The duration histogram of the area, such as <c>crestapps.telnyx.api.request.duration</c>.</param>
    /// <param name="provider">The provider, such as <c>Telnyx</c>.</param>
    /// <param name="operation">The operation or endpoint family, from a bounded set, such as <c>calls.answer</c>.</param>
    /// <returns>The scope; set the status class with <see cref="SetStatusCode(TelemetryScope, int?)"/> and dispose it when the call ends.</returns>
    public static TelemetryScope Start(
        ActivitySource source,
        string spanName,
        Histogram<double> duration,
        string provider,
        string operation)
    {
        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.OperationTag, operation ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        return TelemetryScope.Start(source, spanName, duration, tags, ActivityKind.Client);
    }

    /// <summary>
    /// Records the status code of the response on the span, and its class on the duration histogram. A status
    /// code of 400 or more marks the call as failed.
    /// </summary>
    /// <param name="scope">The scope of the call.</param>
    /// <param name="statusCode">The HTTP status code, or <see langword="null"/> when no response was received.</param>
    public static void SetStatusCode(this TelemetryScope scope, int? statusCode)
    {
        ArgumentNullException.ThrowIfNull(scope);

        scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.StatusClassTag, GetStatusClass(statusCode));

        if (statusCode is { } code)
        {
            scope.SetSpanTag("http.response.status_code", code);

            if (code >= 400)
            {
                scope.Fail();

                return;
            }

            scope.Succeed();
        }
    }

    /// <summary>
    /// Gets the class of an HTTP status code, for the <see cref="CrestAppsOrchardCoreTelemetry.StatusClassTag"/>.
    /// </summary>
    /// <param name="statusCode">The status code, or <see langword="null"/> when no response was received.</param>
    /// <returns><c>1xx</c> to <c>5xx</c>, or <c>none</c>.</returns>
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
