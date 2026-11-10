using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Services;

/// <summary>
/// The log messages of the automated (AI) SMS conversations, with the event ids 18200 to 18299 of the messaging range.
/// </summary>
internal static partial class SmsAutomationLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 200,
        Level = LogLevel.Information,
        Message = "The SMS re-engagement pass checked {Checked} conversation(s): {Sent} nudge(s) sent, {Failed} failed, {HeldBack} held back.")]
    public static partial void ReEngagementPassCompleted(this ILogger logger, int @checked, int sent, int failed, int heldBack);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 201,
        Level = LogLevel.Information,
        Message = "The SMS owed-reply recovery pass checked {Checked} conversation(s): {Redriven} re-driven, {Failed} failed.")]
    public static partial void OwedReplyRecoveryCompleted(this ILogger logger, int @checked, int redriven, int failed);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 202,
        Level = LogLevel.Debug,
        Message = "The automated SMS turn for activity {ActivityId} ended as {Result} after {ElapsedMilliseconds} ms.")]
    public static partial void AITurnEnded(this ILogger logger, string activityId, string result, long elapsedMilliseconds);
}
