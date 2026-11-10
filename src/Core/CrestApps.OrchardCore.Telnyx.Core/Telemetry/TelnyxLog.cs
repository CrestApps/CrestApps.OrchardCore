using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Telemetry;

/// <summary>
/// The source-generated log messages of the Telnyx provider, with event ids from
/// <see cref="CrestAppsEventIds.Telephony"/> + 0 to 199.
/// </summary>
internal static partial class TelnyxLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.Telephony + 1,
        Level = LogLevel.Warning,
        Message = "Telnyx answered {StatusCode} for {Method} {Path}; retrying attempt {Attempt} of {Attempts} after {Delay}.")]
    public static partial void ApiRetrying(ILogger logger, int statusCode, HttpMethod method, string path, int attempt, int attempts, TimeSpan delay);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Telephony + 2,
        Level = LogLevel.Warning,
        Message = "The Telnyx request {Method} {Path} could not reach Telnyx ({ErrorType}); retrying attempt {Attempt} of {Attempts}.")]
    public static partial void ApiRetryingAfterException(ILogger logger, HttpMethod method, string path, string errorType, int attempt, int attempts);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Telephony + 3,
        Level = LogLevel.Information,
        Message = "Telnyx originated a leg to {To} from {From}: CallControlId={CallControlId}.")]
    public static partial void LegOriginated(ILogger logger, string to, string from, string callControlId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Telephony + 11,
        Level = LogLevel.Warning,
        Message = "Telnyx did not accept starting the voicemail recording on call {CallControlId} from the greeting-ended fast path.")]
    public static partial void VoicemailRecordStartFailed(ILogger logger, Exception exception, string callControlId);
}
