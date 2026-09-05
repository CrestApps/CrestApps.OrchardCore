using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
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

            var (outcome, userId) = await ApplyOfferAsync(reservation, cancellationToken);

            switch (outcome)
            {
                case OfferOutcome.Offered:
                    return userId;

                case OfferOutcome.NoAgent:
                    return null;

                case OfferOutcome.NoInteraction:
                    // A preview dial reserves the agent before the call exists - that is what preview means - so
                    // its reservation is kept. Every other kind of outbound work with no interaction is a
                    // reservation holding capacity for a call that will never ring, and is released.
                    var activity = await _activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);

                    if (activity is null ||
                        string.Equals(activity.Source, ActivitySources.Inbound, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(activity.Source, ActivitySources.PreviewDial, StringComparison.OrdinalIgnoreCase))
                    {
                        await _reservationService.RejectAsync(reservation.ItemId, cancellationToken);
                    }

                    return null;

                case OfferOutcome.AlreadyEnded:
                    // A queue has more calls behind this one. Stopping here would leave an available agent idle
                    // with work waiting, which is the one place the queued path must not behave like the direct
                    // one.
                    continue;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<string> OfferToAgentAsync(
        string activityItemId,
        string queueId,
        string agentId,
        int? ringTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);
        ArgumentException.ThrowIfNullOrEmpty(queueId);
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        using var workLease = _workManager.TryEnter(ContactCenterConstants.Feature.Voice);

        if (workLease is null)
        {
            return null;
        }

        var reservation = await _assignmentService.AssignSpecificAsync(activityItemId, queueId, agentId, ringTimeoutSeconds, cancellationToken);

        if (reservation is null)
        {
            // A direct offer fails closed when the named agent is not present/Available, has no live session, or
            // is already on a call. Log it so a direct-to-agent entry point that never rings is diagnosable from
            // the application log instead of only from a live agent session.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "A direct-to-agent offer for activity '{ActivityItemId}' could not be placed because agent '{AgentId}' is not available to take the call.",
                    activityItemId.SanitizeLogValue(),
                    agentId.SanitizeLogValue());
            }

            return null;
        }

        var (outcome, userId) = await ApplyOfferAsync(reservation, cancellationToken);

        if (outcome == OfferOutcome.NoInteraction)
        {
            // There is no next call to try: the caller asked for this agent and this activity.
            await _reservationService.RejectAsync(reservation.ItemId, cancellationToken);

            return null;
        }

        return outcome == OfferOutcome.Offered ? userId : null;
    }

    /// <summary>
    /// The bookkeeping both offer paths share: resolve the reserved agent, find the call, and put it on that
    /// agent's screen. The two cases they treat differently - a reservation with no interaction, and a call that
    /// has already ended - are reported rather than decided here, because a queue has more calls to try and a
    /// direct offer does not.
    /// </summary>
    /// <param name="reservation">The reservation to offer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private async Task<(OfferOutcome Outcome, string UserId)> ApplyOfferAsync(
        ActivityReservation reservation,
        CancellationToken cancellationToken)
    {
        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

        if (agent is null || string.IsNullOrEmpty(agent.UserId))
        {
            // A reservation held for an agent nobody can ring is capacity taken from the agents who could have
            // taken the call.
            await _reservationService.RejectAsync(reservation.ItemId, cancellationToken);

            return (OfferOutcome.NoAgent, null);
        }

        var interaction = await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);

        if (interaction is null)
        {
            return (OfferOutcome.NoInteraction, null);
        }

        if (interaction.Status is InteractionStatus.Ended or InteractionStatus.Failed)
        {
            // Ringing an agent for a call that is already over wastes their time and puts a dead call on screen.
            await _offerSynchronizationService.ReconcileEndedOfferAsync(interaction.ItemId, cancellationToken);

            return (OfferOutcome.AlreadyEnded, null);
        }

        interaction.Reoffer();
        interaction.AgentId = agent.ItemId;
        interaction.QueueId = reservation.QueueId;
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        return (OfferOutcome.Offered, agent.UserId);
    }

    /// <summary>
    /// What happened when a reservation was offered.
    /// </summary>
    private enum OfferOutcome
    {
        /// <summary>
        /// The call is on the agent's screen.
        /// </summary>
        Offered,

        /// <summary>
        /// The reserved agent could not be rung, and the reservation was released.
        /// </summary>
        NoAgent,

        /// <summary>
        /// The reservation has no interaction. Whether that releases the reservation is the caller's decision.
        /// </summary>
        NoInteraction,

        /// <summary>
        /// The call had already ended and was reconciled away.
        /// </summary>
        AlreadyEnded,
    }
}
