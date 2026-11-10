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
        EventId = CrestAppsEventIds.Telephony + 11,
        Level = LogLevel.Warning,
        Message = "Telnyx did not accept starting the voicemail recording on call {CallControlId} from the greeting-ended fast path.")]
    public static partial void VoicemailRecordStartFailed(ILogger logger, Exception exception, string callControlId);
}
