using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Telemetry;

/// <summary>
/// The source-generated log messages of AI voice, with event ids from <see cref="CrestAppsEventIds.Messaging"/> + 500
/// to 999. None of them carries a phone number or what anybody said: numbers go through the address redactor, and a
/// line of the conversation is described by its length only.
/// </summary>
internal static partial class VoiceLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 501,
        Level = LogLevel.Debug,
        Message = "Realtime turn on activity '{ActivityId}': {EventType} ({TextLength} characters).")]
    public static partial void RealtimeTurn(ILogger logger, string activityId, string eventType, int textLength);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 502,
        Level = LogLevel.Debug,
        Message = "Realtime turn on activity '{ActivityId}': {EventType} '{Error}'.")]
    public static partial void RealtimeSessionError(ILogger logger, string activityId, string eventType, string error);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 503,
        Level = LogLevel.Information,
        Message = "AI voice activity '{ActivityId}' found '{PhoneNumber}' not in service ({Detail}); completing it without the assistant.")]
    public static partial void NotInService(ILogger logger, string activityId, string phoneNumber, string detail);
}
