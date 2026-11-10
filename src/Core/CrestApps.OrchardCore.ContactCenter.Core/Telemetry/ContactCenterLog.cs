using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// The source-generated log messages of the Contact Center core services. Their event ids are
/// <see cref="CrestAppsEventIds.ContactCenter"/> to <c>17599</c>; the Contact Center module uses <c>17600</c> to
/// <c>17899</c>. Phone numbers and SIP addresses are passed through the address redactor before they get here.
/// </summary>
internal static partial class ContactCenterLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.ContactCenter,
        Level = LogLevel.Information,
        Message = "Received a provider voice event for call '{ProviderCallId}' that does not match any interaction. State={State}, HangupCause={HangupCause}, From={FromAddress}, To={ToAddress}.")]
    public static partial void UnmatchedProviderVoiceEvent(
        ILogger logger,
        string providerCallId,
        VoiceCallState state,
        HangupCause? hangupCause,
        string fromAddress,
        string toAddress);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContactCenter + 1,
        Level = LogLevel.Warning,
        Message = "Provider voice event for call '{ProviderCallId}' lost a concurrency race; retrying it in a fresh scope (attempt {Attempt} of {MaxAttempts}).")]
    public static partial void ProviderVoiceEventRetried(ILogger logger, string providerCallId, int attempt, int maxAttempts);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContactCenter + 2,
        Level = LogLevel.Information,
        Message = "Dialer call '{InteractionId}' for activity '{ActivityId}' found '{PhoneNumber}' not in service ({Reason}); completing the activity without an agent.")]
    public static partial void DialerNumberNotInService(
        ILogger logger,
        string interactionId,
        string activityId,
        string phoneNumber,
        string reason);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContactCenter + 100,
        Message = "Provider command '{ProviderCommandId}' ({CommandType}) to provider '{ProviderName}' settled as {Status} after {Attempts} attempt(s).")]
    public static partial void ProviderCommandSettled(
        ILogger logger,
        LogLevel level,
        string providerCommandId,
        ProviderCommandType commandType,
        string providerName,
        ProviderCommandStatus status,
        int attempts);
}
