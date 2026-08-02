using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Default <see cref="IVoiceQueueOfferService"/> implementation. The provider event stream and reconciliation
/// service own provider truth, so offering work remains a local atomic transition.
/// </summary>
public sealed class VoiceQueueOfferService : IVoiceQueueOfferService
{
    private const int MaxOfferAttempts = 25;
    private readonly IActivityAssignmentService _assignmentService;
    private readonly IActivityReservationService _reservationService;
    private readonly IAgentProfileManager _agentManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IProviderVoiceOfferSynchronizationService _offerSynchronizationService;
    private readonly IContactCenterFeatureWorkManager _workManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceQueueOfferService"/> class.
    /// </summary>
    /// <param name="assignmentService">The assignment service used to reserve an available agent.</param>
    /// <param name="reservationService">The reservation service used to release invalid offers.</param>
    /// <param name="agentManager">The agent profile manager used to resolve the reserved agent.</param>
    /// <param name="interactionManager">The interaction manager used to record communication history.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="offerSynchronizationService">The offer synchronization service used to remove calls already known to have ended.</param>
    /// <param name="workManager">The feature work manager used to reject offering while Voice is quiescing.</param>
    public VoiceQueueOfferService(
        IActivityAssignmentService assignmentService,
        IActivityReservationService reservationService,
        IAgentProfileManager agentManager,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IProviderVoiceOfferSynchronizationService offerSynchronizationService,
        IContactCenterFeatureWorkManager workManager)
    {
        _assignmentService = assignmentService;
        _reservationService = reservationService;
        _agentManager = agentManager;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _offerSynchronizationService = offerSynchronizationService;
        _workManager = workManager;
    }

    /// <inheritdoc/>
    public async Task<string> OfferNextAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        using var workLease = _workManager.TryEnter(ContactCenterConstants.Feature.Voice);

        if (workLease is null)
        {
            return null;
        }

        // The provider event stream and reconciliation service own provider truth. Offering work must remain
        // a local atomic transition so provider latency or transport failure cannot strand an uncommitted reservation.
        for (var attempt = 0; attempt < MaxOfferAttempts; attempt++)
        {
            var reservation = await _assignmentService.AssignNextAsync(queueId, cancellationToken);

            if (reservation is null)
            {
                return null;
            }

            var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

            if (agent is null || string.IsNullOrEmpty(agent.UserId))
            {
                await _reservationService.RejectAsync(reservation.ItemId, cancellationToken);

                return null;
            }

            var interaction = await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);

            if (interaction is null)
            {
                var activity = await _activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);

                if (activity is not null &&
                    !string.Equals(activity.Source, ActivitySources.Inbound, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(activity.Source, ActivitySources.PreviewDial, StringComparison.OrdinalIgnoreCase))
                    {
                        await _reservationService.RejectAsync(reservation.ItemId, cancellationToken);
                    }

                    return null;
                }

                await _reservationService.RejectAsync(reservation.ItemId, cancellationToken);

                return null;
            }

            if (interaction.Status is InteractionStatus.Ended or InteractionStatus.Failed)
            {
                await _offerSynchronizationService.ReconcileEndedOfferAsync(interaction.ItemId, cancellationToken);

                continue;
            }

            interaction.Reoffer();
            interaction.AgentId = agent.ItemId;
            interaction.QueueId = reservation.QueueId;
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

            return agent.UserId;
        }

        return null;
    }
}
