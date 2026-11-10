using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Telemetry;

/// <summary>
/// The log messages of the SMS channel, with the event ids 18100 to 18199 of the messaging range.
/// </summary>
internal static partial class SmsLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 100,
        Level = LogLevel.Warning,
        Message = "No SMS provider could be resolved for the sending number or the tenant default, so the message was not sent.")]
    public static partial void NoProviderResolved(this ILogger logger);
}
