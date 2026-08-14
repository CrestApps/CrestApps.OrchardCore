using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Default <see cref="IVoiceQueueOfferService"/> implementation. The provider event stream and reconciliation
/// service own provider truth, so offering work remains a local atomic transition.
/// </summary>
public sealed class VoiceQueueOfferService : IVoiceQueueOfferService
{
    private const int MaxOfferAttempts = 25;

    // The bounded number of due reservations reclaimed opportunistically before an offer. Kept deliberately
    // small so the pass adds at most a handful of quick transitions, each guarded by a short bounded lock wait,
    // to the latency-sensitive admission path - enough to free capacity for this and the next few offers on a
    // busy queue, while the scheduled backstop sweep drains anything beyond it.
    private const int MaxReclaimPerOffer = 4;
    private readonly IActivityAssignmentService _assignmentService;
    private readonly IActivityReservationService _reservationService;
    private readonly IActivityReservationReclaimer _reservationReclaimer;
    private readonly IAgentProfileManager _agentManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IProviderVoiceOfferSynchronizationService _offerSynchronizationService;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceQueueOfferService"/> class.
    /// </summary>
    /// <param name="assignmentService">The assignment service used to reserve an available agent.</param>
    /// <param name="reservationService">The reservation service used to release invalid offers.</param>
    /// <param name="reservationReclaimer">The reclaimer used to opportunistically free stale reservations before offering.</param>
    /// <param name="agentManager">The agent profile manager used to resolve the reserved agent.</param>
    /// <param name="interactionManager">The interaction manager used to record communication history.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="offerSynchronizationService">The offer synchronization service used to remove calls already known to have ended.</param>
    /// <param name="workManager">The feature work manager used to reject offering while Voice is quiescing.</param>
    /// <param name="logger">The logger instance.</param>
    public VoiceQueueOfferService(
        IActivityAssignmentService assignmentService,
        IActivityReservationService reservationService,
        IActivityReservationReclaimer reservationReclaimer,
        IAgentProfileManager agentManager,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IProviderVoiceOfferSynchronizationService offerSynchronizationService,
        IContactCenterFeatureWorkManager workManager,
        ILogger<VoiceQueueOfferService> logger)
    {
        _assignmentService = assignmentService;
        _reservationService = reservationService;
        _reservationReclaimer = reservationReclaimer;
        _agentManager = agentManager;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _offerSynchronizationService = offerSynchronizationService;
        _workManager = workManager;
        _logger = logger;
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

        // Opportunistically reclaim due reservations before selecting an agent. Reject and cancel already
        // release a reservation synchronously, so the only capacity a stale reservation can hold is an offer an
        // agent silently ignored past its timeout. Whenever an offer runs for this queue we take the chance to
        // free that capacity immediately instead of leaving it parked until the next minute sweep - valuable on
        // a busy queue where offers keep arriving. This is a bounded pass with only a short lock wait: it
        // examines only the oldest page of due reservations and skips - rather than blocking indefinitely on -
        // one another node is already transitioning, so it adds at most a small, bounded lock wait per contended
        // candidate to admitting the call. It is best-effort - any hiccup is swallowed
        // and the offer proceeds. It is deliberately not a deadline mechanism: an ignored offer on an otherwise
        // idle queue is still reclaimed by the scheduled ReservationExpiryBackgroundTask, which remains the
        // authoritative backstop.
        try
        {
            await _reservationReclaimer.ReclaimDueAsync(MaxReclaimPerOffer, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A best-effort reservation reclaim before offering queue '{QueueId}' did not complete; relying on the scheduled sweep.", queueId.SanitizeLogValue());
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
