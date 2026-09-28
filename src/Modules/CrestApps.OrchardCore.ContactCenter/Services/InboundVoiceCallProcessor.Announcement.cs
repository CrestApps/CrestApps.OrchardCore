using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The part of inbound routing that says an entry point's welcome or closed message before the caller goes on, and
/// ends a closed caller once that message has been said.
/// </summary>
public sealed partial class InboundVoiceCallProcessor
{
    private const string WelcomeReasonCode = "entry_point_welcome";
    private const string ClosedMessageReasonCode = "entry_point_closed_message";

    /// <inheritdoc/>
    public async Task<bool> EndInboundAsync(string activityItemId, string reasonCode, bool providerEndsCall, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);
        ArgumentException.ThrowIfNullOrEmpty(reasonCode);

        var queueItem = await _queueItemManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        // A caller who is waiting, offered or taken is somebody else's to end.
        if (queueItem?.Status is QueueItemStatus.Waiting or QueueItemStatus.Reserved or QueueItemStatus.Assigned)
        {
            return false;
        }

        var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);
        var interaction = await _interactionManager.FindByActivityIdAsync(activityItemId, cancellationToken);

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
            ActivityStatus.Cancelled,
            InteractionStatus.Ended,
            reasonCode,
            providerEndsCall ? null : ProviderCommandType.Reject,
            _clock.UtcNow,
            cancellationToken);

        return true;
    }

    // Which message, if any, the caller hears before they go on, and where they go once it has been said. Nothing is
    // said when the message is empty, and nothing is said to a caller who has nowhere to go afterwards: they are
    // turned away exactly as before, rather than welcomed and then dropped.
    private EntryPointAnnouncement ResolveAnnouncement(EntryPointRoutingPlan plan, bool hasMenu, bool isDirect, ActivityQueue queue)
    {
        var entryPoint = plan?.EntryPoint;

        if (entryPoint is null)
        {
            return null;
        }

        if (plan.IsOpen)
        {
            if (string.IsNullOrWhiteSpace(entryPoint.WelcomeMessage))
            {
                LogNoAnnouncement(entryPoint, EntryPointAnnouncement.Welcome);

                return null;
            }

            if (hasMenu)
            {
                return new EntryPointAnnouncement { Kind = EntryPointAnnouncement.Welcome, Next = EntryPointAnnouncement.NextMenu };
            }

            if (isDirect || queue is not null)
            {
                return new EntryPointAnnouncement { Kind = EntryPointAnnouncement.Welcome, Next = EntryPointAnnouncement.NextTarget };
            }

            LogAnnouncementWithoutDestination(entryPoint, EntryPointAnnouncement.Welcome);

            return null;
        }

        if (string.IsNullOrWhiteSpace(entryPoint.ClosedMessage))
        {
            LogNoAnnouncement(entryPoint, EntryPointAnnouncement.Closed);

            return null;
        }

        if (plan.ShouldQueue)
        {
            if (queue is null)
            {
                LogAnnouncementWithoutDestination(entryPoint, EntryPointAnnouncement.Closed);

                return null;
            }

            return new EntryPointAnnouncement
            {
                Kind = EntryPointAnnouncement.Closed,
                Next = EntryPointAnnouncement.NextQueue,
                QueueId = queue.ItemId,
            };
        }

        return new EntryPointAnnouncement
        {
            Kind = EntryPointAnnouncement.Closed,
            Next = plan.ClosedAction == EntryPointClosedAction.Voicemail
                ? EntryPointAnnouncement.NextVoicemail
                : EntryPointAnnouncement.NextReject,
        };
    }

    private async Task<InboundVoiceRoutingResult> StartAnnouncementAsync(
        EntryPointRoutingPlan plan,
        Core.Models.Interaction interaction,
        EntryPointAnnouncement announcement,
        InboundVoiceRoutingResult result,
        CancellationToken cancellationToken)
    {
        // The entry point is recorded for the same reason a menu records it: the caller keeps the entry point they rang
        // even if the number is re-pointed while they are listening to its message.
        interaction.TechnicalMetadata[EntryPointFlowResolver.EntryPointMetadataKey] = plan.EntryPoint.ItemId;

        // Recording the entry point is also what tells the digits sink a caller may be in its menu. A caller who is not
        // going to hear the menu (a closed entry point, or one whose menu has no first menu) is recorded as having none
        // left, or the key they press on a queue's callback offer would be read as a menu choice.
        if (announcement.Next != EntryPointAnnouncement.NextMenu)
        {
            interaction.TechnicalMetadata[IvrExecutionService.StateMetadataKey] = new IvrFlowState { Completed = true };
        }

        EntryPointAnnouncement.Schedule(interaction, announcement.Kind, announcement.Next, announcement.QueueId);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        var interactionId = interaction.ItemId;

        // Said once this routing commits, like the menu: answering and speaking inside the routing transaction would let
        // the provider's events for the call arrive for an interaction the store does not have yet.
        _scopeExecutor.ScheduleAfterCommit<IIvrCallRouter>(router => router.AnnounceAsync(interactionId, CancellationToken.None));

        var isWelcome = announcement.Kind == EntryPointAnnouncement.Welcome;

        result.Reason = isWelcome
            ? "The caller is hearing the entry point's welcome message."
            : "The caller is hearing the entry point's closed message.";
        result.ReasonCode = isWelcome ? WelcomeReasonCode : ClosedMessageReasonCode;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Interaction '{InteractionId}' on entry point '{EntryPointId}' is owed the entry point's {AnnouncementKind} message; once it has been said the caller goes on to {AnnouncementNext}.",
                interactionId.SanitizeLogValue(),
                plan.EntryPoint.ItemId.SanitizeLogValue(),
                announcement.Kind,
                announcement.Next);
        }

        return result;
    }

    private void LogNoAnnouncement(ContactCenterEntryPoint entryPoint, string kind)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Entry point '{EntryPointId}' has no {AnnouncementKind} message, so the caller goes straight on.",
                entryPoint.ItemId.SanitizeLogValue(),
                kind);
        }
    }

    private void LogAnnouncementWithoutDestination(ContactCenterEntryPoint entryPoint, string kind)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The {AnnouncementKind} message of entry point '{EntryPointId}' is not said because the entry point has no reachable target queue; the call is handled as an unroutable call.",
                kind,
                entryPoint.ItemId.SanitizeLogValue());
        }
    }
}
