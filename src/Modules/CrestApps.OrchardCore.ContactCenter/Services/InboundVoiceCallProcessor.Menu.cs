using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The part of inbound routing that hands a caller to an entry point's phone menu, and takes a caller the menu is
/// finished with to voicemail.
/// </summary>
public sealed partial class InboundVoiceCallProcessor
{
    private const string MenuReasonCode = "ivr_menu";

    /// <inheritdoc/>
    public async Task<bool> SendToVoicemailAsync(string activityItemId, string reasonCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);
        ArgumentException.ThrowIfNullOrEmpty(reasonCode);

        var queueItem = await _queueItemManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        // Offered or reserved, the reservation owns the call; waiting, it has to leave its queue on the way out.
        if (queueItem?.Status is QueueItemStatus.Reserved or QueueItemStatus.Assigned)
        {
            return false;
        }

        if (queueItem?.Status == QueueItemStatus.Waiting)
        {
            return await SendWaitingItemToVoicemailAsync(queueItem, reasonCode, cancellationToken);
        }

        var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);
        var interaction = await _interactionManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        // Only a live voice call can be sent to voicemail; one that has already ended has nobody to leave a message.
        if (activity is null ||
            interaction is null ||
            interaction.Channel != InteractionChannel.Voice ||
            interaction.IsSettled)
        {
            return false;
        }

        await TerminalizeInboundAsync(
            activity,
            interaction,
            ActivityStatus.Completed,
            InteractionStatus.Ended,
            reasonCode,
            ProviderCommandType.SendToVoicemail,
            _clock.UtcNow,
            cancellationToken);

        return true;
    }

    // A menu is only played on an open entry point: business hours and the closed action decide first, so a caller
    // who rings after hours gets what the entry point does after hours, not a menu of teams that have gone home.
    private static bool HasMenu(EntryPointRoutingPlan plan)
    {
        var flow = plan is { IsOpen: true, ShouldQueue: true } ? plan.EntryPoint?.IvrFlow : null;

        return flow is not null &&
            !string.IsNullOrEmpty(flow.RootNodeId) &&
            flow.Nodes.Any(node => string.Equals(node?.NodeId, flow.RootNodeId, StringComparison.Ordinal));
    }

    private async Task<InboundVoiceRoutingResult> StartMenuAsync(
        EntryPointRoutingPlan plan,
        Core.Models.Interaction interaction,
        InboundVoiceRoutingResult result,
        CancellationToken cancellationToken)
    {
        // The entry point is recorded rather than re-derived later from the dialled number, so a caller keeps the
        // menu they started even if the number is re-pointed while they are listening to it.
        interaction.TechnicalMetadata[EntryPointFlowResolver.EntryPointMetadataKey] = plan.EntryPoint.ItemId;
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        var interactionId = interaction.ItemId;

        // The menu is played once this routing commits. Answering and prompting inside the routing transaction would
        // let the provider's answered event arrive for a call the store does not have yet. Inbound routing always
        // runs in a shell scope, which is what the deferral needs.
        _scopeExecutor.ScheduleAfterCommit<IIvrCallRouter>(router => router.StartAsync(interactionId, CancellationToken.None));

        result.Reason = "The caller is hearing the entry point's phone menu.";
        result.ReasonCode = MenuReasonCode;

        return result;
    }
}
