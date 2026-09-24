using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// What a dial writes to the audit log: that the provider placed it, or why it could not be placed.
/// </summary>
public sealed partial class DialProviderCommandTypeExecutor
{
    private Task RecordDialStartedAsync(
        ProviderCommand command,
        ContactCenterVoiceProviderResult result,
        Interaction interaction,
        CallSession session,
        CancellationToken cancellationToken)
    {
        var data = CreateDialData(command, interaction, session);
        data.ProviderName = string.IsNullOrWhiteSpace(result.ProviderName) ? command.ProviderName : result.ProviderName;
        data.ProviderCallId = result.ProviderCallId ?? data.ProviderCallId;
        data.State = nameof(VoiceCallState.Ringing);

        // One dial per command: a projection retried after a crash is the same dial.
        return _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.DialStarted,
            data,
            _clock.UtcNow,
            ContactCenterActor.System,
            $"dial:{ContactCenterConstants.Events.DialStarted}:{command.CommandId}",
            cancellationToken);
    }

    private Task RecordDialFailedAsync(
        ProviderCommand command,
        Interaction interaction,
        CancellationToken cancellationToken)
    {
        var data = CreateDialData(command, interaction, session: null);
        data.State = nameof(VoiceCallState.Failed);
        data.Reason = string.IsNullOrWhiteSpace(command.LastError)
            ? nameof(ProviderCommandStatus.Failed)
            : command.LastError;

        return _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.DialFailed,
            data,
            _clock.UtcNow,
            ContactCenterActor.System,
            $"dial:{ContactCenterConstants.Events.DialFailed}:{command.CommandId}",
            cancellationToken);
    }

    private static CallLifecycleEventData CreateDialData(ProviderCommand command, Interaction interaction, CallSession session)
    {
        var data = session is not null
            ? ContactCenterCallAudit.ForSession(session, interaction)
            : interaction is not null
                ? ContactCenterCallAudit.ForInteraction(interaction)
                : new CallLifecycleEventData();

        data.InteractionId ??= command.InteractionId;
        data.ActivityItemId ??= command.ActivityItemId;
        data.ProviderName ??= command.ProviderName;
        data.Direction ??= nameof(InteractionDirection.Outbound);
        data.Details["commandId"] = command.CommandId;

        if (!string.IsNullOrWhiteSpace(command.DialerProfileId))
        {
            data.Details["dialerProfileId"] = command.DialerProfileId;
        }

        return data;
    }
}
