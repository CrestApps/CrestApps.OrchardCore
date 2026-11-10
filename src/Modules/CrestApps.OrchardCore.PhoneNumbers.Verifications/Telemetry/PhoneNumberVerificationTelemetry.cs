using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Telemetry;

/// <summary>
/// The OpenTelemetry spans and metrics of phone number verification lookups. The meter and activity source are both
/// named <see cref="SourceName"/>. The number looked up never reaches a metric, a span or a log line.
/// </summary>
public static class PhoneNumberVerificationTelemetry
{
    /// <summary>
    /// The name of the meter and of the activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".PhoneNumbers";

    /// <summary>
    /// The area of the phone verification retries on <c>crestapps.retry.attempts</c>.
    /// </summary>
    public const string RetryArea = "phone_verification";

    /// <summary>
    /// Gets the meter of phone number verification.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("PhoneNumbers");

    /// <summary>
    /// Gets the activity source of phone number verification.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("PhoneNumbers");

    /// <summary>
    /// The duration of one verification lookup, tagged with the <c>provider</c> (the configured provider key), the
    /// <c>result</c> (<c>verified</c>, <c>invalid</c>, <c>failed</c> or <c>unverified</c>) and the <c>outcome</c>.
    /// Its count is the number of lookups.
    /// </summary>
    public static Histogram<double> VerificationDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.phonenumbers.verification.duration",
        "The duration of phone number verification lookups.");

    /// <summary>
    /// Starts measuring one verification lookup.
    /// </summary>
    /// <param name="contentItemId">The record being verified, set on the span only.</param>
    /// <returns>The scope of the lookup.</returns>
    public static TelemetryScope StartVerification(string contentItemId)
    {
        var scope = TelemetryScope.Start(ActivitySource, "crestapps.phonenumbers.verification", VerificationDuration);

        scope.SetSpanTag("crestapps.content_item.id", contentItemId);

        return scope;
    }

    /// <summary>
    /// Records how a lookup ended on its scope.
    /// </summary>
    /// <param name="scope">The scope started with <see cref="StartVerification(string)"/>.</param>
    /// <param name="result">The lookup's result, or <see langword="null"/> when there is none.</param>
    public static void CompleteVerification(TelemetryScope scope, PhoneNumberVerificationResult result)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var status = result?.Status ?? PhoneNumberVerificationStatus.Failed;

        scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ProviderTag, string.IsNullOrWhiteSpace(result?.VerificationProvider) ? CrestAppsOrchardCoreTelemetry.Unknown : result.VerificationProvider);
        scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ResultTag, GetResult(status));

        if (status == PhoneNumberVerificationStatus.Failed)
        {
            scope.Fail();

            return;
        }

        scope.Succeed();
    }

    /// <summary>
    /// Gets the <c>result</c> tag value of a verification status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The bounded value.</returns>
    public static string GetResult(PhoneNumberVerificationStatus status)
    {
        return status switch
        {
            PhoneNumberVerificationStatus.Verified => "verified",
            PhoneNumberVerificationStatus.Invalid => "invalid",
            PhoneNumberVerificationStatus.Failed => "failed",
            _ => "unverified",
        };
    }
}
