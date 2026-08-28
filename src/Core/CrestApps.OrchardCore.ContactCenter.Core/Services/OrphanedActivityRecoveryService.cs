using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IOrphanedActivityRecoveryService"/>.
/// </summary>
public sealed class OrphanedActivityRecoveryService : IOrphanedActivityRecoveryService
{
    // The activity statuses the campaign report aggregates as "in progress". These are the states a record can be
    // stranded in when the reservation/interaction that owned it is released without the activity being rolled
    // forward to a terminal state or back to Pending.
    private static readonly ActivityStatus[] _intermediateStatuses =
    [
        ActivityStatus.Reserved,
        ActivityStatus.Dialing,
        ActivityStatus.AwaitingAgentResponse,
        ActivityStatus.AwaitingCustomerAnswer,
        ActivityStatus.InProgress,
    ];

    private const string OrphanRecoveredReasonCode = "orphaned-recovered";

    private readonly ISession _session;
    private readonly IInteractionManager _interactionManager;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IContactCenterActivityWriter _activityWriter;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IActivityQueueService _queueService;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrphanedActivityRecoveryService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to find stale intermediate-status activities.</param>
    /// <param name="interactionManager">The interaction manager used to confirm no live call is attached.</param>
    /// <param name="reservationManager">The reservation manager used to confirm no live reservation is attached.</param>
    /// <param name="activityWriter">The writer used to roll the activity forward or back with concurrency retry.</param>
    /// <param name="workStateService">The work-state service used to clear the routing projection.</param>
    /// <param name="queueService">The queue service used to re-enqueue a recovered activity.</param>
    /// <param name="queueItemManager">The queue item manager used to drop any lingering queue item.</param>
    /// <param name="clock">The clock used to evaluate staleness and stamp completion.</param>
    /// <param name="logger">The logger.</param>
    public OrphanedActivityRecoveryService(
        ISession session,
        IInteractionManager interactionManager,
        IActivityReservationManager reservationManager,
        IContactCenterActivityWriter activityWriter,
        IContactCenterWorkStateService workStateService,
        IActivityQueueService queueService,
        IQueueItemManager queueItemManager,
        IClock clock,
        ILogger<OrphanedActivityRecoveryService> logger)
    {
        _session = session;
        _interactionManager = interactionManager;
        _reservationManager = reservationManager;
        _activityWriter = activityWriter;
        _workStateService = workStateService;
        _queueService = queueService;
        _queueItemManager = queueItemManager;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> RecoverAsync(TimeSpan gracePeriod, int maxToRecover, CancellationToken cancellationToken = default)
    {
        if (maxToRecover <= 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var cutoff = now - gracePeriod;

        // Bound the scan to records that have not been reserved recently. The interaction and reservation checks
        // below are what actually make a live call safe from recovery; this predicate only keeps the candidate
        // set small on a busy campaign, where most intermediate-status records are fresh active offers.
        var candidates = await _session
            .Query<OmnichannelActivity, OmnichannelActivityIndex>(
                index => index.Status.IsIn(_intermediateStatuses) &&
                    (index.ReservedUtc == null || index.ReservedUtc < cutoff),
                collection: OmnichannelConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Take(maxToRecover)
            .ListAsync(cancellationToken);

        return await RecoverCandidatesAsync(candidates, now, cancellationToken);
    }

    /// <summary>
    /// Runs the recovery decision for each candidate, isolating a failure to the one record it happened on so a
    /// single bad activity cannot abort the batch. Exposed to the test project so the decision matrix can be
    /// verified without a live YesSql session.
    /// </summary>
    internal async Task<int> RecoverCandidatesAsync(IEnumerable<OmnichannelActivity> candidates, DateTime now, CancellationToken cancellationToken)
    {
        var recovered = 0;

        foreach (var activity in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (await RecoverOneAsync(activity, now, cancellationToken))
                {
                    recovered++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to recover orphaned Contact Center activity '{ActivityItemId}'.",
                    activity.ItemId.SanitizeLogValue());
            }
        }

        return recovered;
    }

    private async Task<bool> RecoverOneAsync(OmnichannelActivity activity, DateTime now, CancellationToken cancellationToken)
    {
        // A live reservation means this is still a ringing offer the reservation-expiry sweep owns, not an orphan.
        if (!string.IsNullOrEmpty(activity.ReservationId))
        {
            var reservation = await _reservationManager.FindByIdAsync(activity.ReservationId, cancellationToken);

            if (reservation is not null && !reservation.IsResolved && reservation.ExpiresUtc > now)
            {
                return false;
            }
        }

        // An unsettled interaction means a call is still live (or is a long-running connected call). Never touch
        // it - this is the guard that keeps a slow but genuine call safe regardless of how old its reservation is.
        var interaction = await _interactionManager.FindByActivityIdAsync(activity.ItemId, cancellationToken);

        if (interaction is not null && !interaction.IsSettled)
        {
            return false;
        }

        // A record that reached InProgress, or whose interaction was answered, may already have reached the
        // customer. Never re-dial it; move it to a terminal state so it stops inflating the in-progress count. A
        // record still in a pre-answer status (Reserved/Dialing/Awaiting*) with no answered interaction was never
        // connected, so it is safe to return to Pending for a fresh offer.
        var wasConnected =
            activity.Status == ActivityStatus.InProgress ||
            interaction?.AnsweredUtc is not null;

        if (wasConnected)
        {
            await TerminateAsync(activity, now, cancellationToken);
        }
        else
        {
            await RequeueAsync(activity, cancellationToken);
        }

        return true;
    }

    private async Task TerminateAsync(OmnichannelActivity activity, DateTime now, CancellationToken cancellationToken)
    {
        await DropQueueItemAsync(activity.ItemId, requeue: false, cancellationToken);

        var workState = await ClearWorkStateAsync(activity.ItemId, ActivityAssignmentStatus.Released, cancellationToken);

        await _activityWriter.UpdateAsync(activity.ItemId, target =>
        {
            target.Status = ActivityStatus.Failed;
            target.TerminalReasonCode = OrphanRecoveredReasonCode;
            target.CompletedUtc = now;
            ContactCenterWorkStateProjector.Apply(target, workState);
        }, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Recovered orphaned Contact Center activity '{ActivityItemId}' to Failed because it may have connected; it was not re-dialed.",
                activity.ItemId.SanitizeLogValue());
        }
    }

    private async Task RequeueAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        var workState = await ClearWorkStateAsync(activity.ItemId, ActivityAssignmentStatus.Available, cancellationToken);

        await _activityWriter.UpdateAsync(activity.ItemId, target =>
        {
            target.Status = ActivityStatus.Pending;
            ContactCenterWorkStateProjector.Apply(target, workState);
        }, cancellationToken);

        // Drop any lingering queue item so a fresh Waiting item can be created, then re-enqueue onto the campaign
        // queue so an available agent is offered the record again.
        await DropQueueItemAsync(activity.ItemId, requeue: true, cancellationToken);

        if (!string.IsNullOrEmpty(activity.CampaignId))
        {
            var queueId = ContactCenterConstants.CampaignQueue.CreateId(activity.CampaignId);
            await _queueService.EnqueueAsync(activity.ItemId, queueId, priority: null, cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Recovered orphaned Contact Center activity '{ActivityItemId}' to Pending and re-queued it; it was never answered.",
                activity.ItemId.SanitizeLogValue());
        }
    }

    private async Task DropQueueItemAsync(string activityItemId, bool requeue, CancellationToken cancellationToken)
    {
        var queueItem = await _queueItemManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (queueItem is null)
        {
            return;
        }

        // For a re-queue we only need to clear a stale reserved/assigned item so EnqueueAsync will create a fresh
        // Waiting one (it returns any existing Waiting/Reserved/Assigned item untouched). For a terminate we drop
        // any still-active item so nothing offers the now-Failed activity.
        var isActive = queueItem.Status is QueueItemStatus.Waiting or QueueItemStatus.Reserved or QueueItemStatus.Assigned;

        if (requeue && queueItem.Status == QueueItemStatus.Waiting)
        {
            return;
        }

        if (!isActive)
        {
            return;
        }

        queueItem.ReservationId = null;
        queueItem.AgentId = null;

        await _queueService.DequeueAsync(queueItem, QueueItemStatus.Removed, cancellationToken);
    }

    private async Task<ContactCenterWorkState> ClearWorkStateAsync(string activityItemId, ActivityAssignmentStatus target, CancellationToken cancellationToken)
    {
        return await _workStateService.MutateAsync(activityItemId, workState =>
        {
            workState.ReservationId = null;
            workState.ReservedById = null;
            workState.ReservedByUsername = null;
            workState.ReservedUtc = null;
            workState.ReservationExpiresUtc = null;

            if (workState.AssignmentStatus != target && workState.CanTransitionTo(target))
            {
                workState.TransitionTo(target);
            }
        }, cancellationToken);
    }
}
