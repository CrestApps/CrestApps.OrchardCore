using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IDialerDispatchDiagnosticsService"/>.
/// </summary>
/// <remarks>
/// A campaign's pending records are read the way the queue gate reads them (<see cref="DialerWorkHoldEvaluator"/> is the one
/// place that decides a profile is off, a record is out of attempts, scheduled for later or cooling down), so what is shown
/// is the decision dispatch makes. The compliance gate's reasons cannot be evaluated here without contacting registries, so
/// they are read from the suppression events the dialer recorded; routing's reasons are read from its decision events.
/// </remarks>
public sealed class DialerDispatchDiagnosticsService : IDialerDispatchDiagnosticsService
{
    /// <summary>
    /// The most pending records of one campaign a pass evaluates; the rest are counted as pending but not explained.
    /// </summary>
    internal const int MaxEvaluatedRecords = 2000;

    /// <summary>
    /// How far back a suppression counts as why a record is not dialing. The dialer retries a suppressed record on its
    /// next pass, so an older one says nothing about now.
    /// </summary>
    internal static readonly TimeSpan SuppressionLookback = TimeSpan.FromHours(2);

    /// <summary>
    /// How far back a routing decision counts as why a queue is not assigning. Routing decides on every pass, so an older
    /// one says nothing about now.
    /// </summary>
    internal static readonly TimeSpan RoutingLookback = TimeSpan.FromMinutes(15);

    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IBusinessHoursService _businessHours;
    private readonly IInteractionEventStore _eventStore;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly DialerRecordClassifier _classifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerDispatchDiagnosticsService"/> class.
    /// </summary>
    /// <param name="queueManager">The stored queues.</param>
    /// <param name="queueItemManager">The queue items waiting in each queue.</param>
    /// <param name="businessHours">The business-hours service that pauses a closed queue.</param>
    /// <param name="profileReader">The reader of the dialer profile a record is dialed under.</param>
    /// <param name="activityManager">The CRM activities the campaign records are.</param>
    /// <param name="workStateStore">The work state that holds each record's attempt count.</param>
    /// <param name="eventStore">The recorded suppression and routing decisions.</param>
    /// <param name="session">The YesSql session, which reads many records' pending state and last calls at once.</param>
    /// <param name="clock">The clock.</param>
    public DialerDispatchDiagnosticsService(
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IBusinessHoursService businessHours,
        IDialerProfileReader profileReader,
        IOmnichannelActivityManager activityManager,
        IContactCenterWorkStateStore workStateStore,
        IInteractionEventStore eventStore,
        ISession session,
        IClock clock)
    {
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _businessHours = businessHours;
        _eventStore = eventStore;
        _session = session;
        _clock = clock;
        _classifier = new DialerRecordClassifier(profileReader, activityManager, workStateStore, eventStore, session);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<QueueDispatchDiagnostics>> DiagnoseAsync(
        IReadOnlyCollection<string> queueIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueIds);

        var ids = queueIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var now = _clock.UtcNow;
        var findings = await ReadRoutingFindingsAsync(ids, now, cancellationToken);
        var results = new List<QueueDispatchDiagnostics>(ids.Length);

        foreach (var queueId in ids)
        {
            var diagnostics = ContactCenterConstants.CampaignQueue.GetCampaignId(queueId) is { } campaignId
                ? await DiagnoseCampaignAsync(queueId, campaignId, now, cancellationToken)
                : await DiagnoseQueueAsync(queueId, now, cancellationToken);

            diagnostics.RoutingFinding = findings.GetValueOrDefault(queueId);
            results.Add(diagnostics);
        }

        return results;
    }

    private async Task<QueueDispatchDiagnostics> DiagnoseQueueAsync(string queueId, DateTime now, CancellationToken cancellationToken)
    {
        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);
        var waiting = await _queueItemManager.CountWaitingAsync(queueId, cancellationToken);

        // The order assignment checks them in: a queue that is off, then one whose business hours are closed.
        DispatchBlocker? queueBlocker = null;

        if (queue is null || !queue.Enabled)
        {
            queueBlocker = DispatchBlocker.QueueDisabled;
        }
        else if (!await _businessHours.IsOpenAsync(queue.BusinessHoursCalendarId, now, cancellationToken))
        {
            queueBlocker = DispatchBlocker.BusinessHoursClosed;
        }

        var diagnostics = new QueueDispatchDiagnostics
        {
            QueueId = queueId,
            PendingCount = waiting,
            EvaluatedCount = waiting,
            QueueBlocker = queueBlocker,
        };

        if (waiting > 0)
        {
            diagnostics.Buckets.Add(new DispatchBucket
            {
                Blocker = queueBlocker ?? DispatchBlocker.DialableNow,
                Count = waiting,
            });
        }

        Tally(diagnostics);

        return diagnostics;
    }

    private async Task<QueueDispatchDiagnostics> DiagnoseCampaignAsync(string queueId, string campaignId, DateTime now, CancellationToken cancellationToken)
    {
        var pending = _session.QueryIndex<OmnichannelActivityIndex>(
            index => index.CampaignId == campaignId &&
                (index.Status == ActivityStatus.NotStated || index.Status == ActivityStatus.Pending || index.Status == ActivityStatus.Scheduled),
            collection: OmnichannelConstants.CollectionName);

        var diagnostics = new QueueDispatchDiagnostics
        {
            QueueId = queueId,
            CampaignId = campaignId,
            PendingCount = await pending.CountAsync(cancellationToken),
        };

        var waitingItems = await _queueItemManager.GetWaitingAsync(queueId, cancellationToken);

        diagnostics.WaitingItemCount = waitingItems.Count;
        diagnostics.ReservedItemCount = await CountItemsAsync(queueId, QueueItemStatus.Reserved, cancellationToken);
        diagnostics.AssignedItemCount = await CountItemsAsync(queueId, QueueItemStatus.Assigned, cancellationToken);

        var stampProfiles = new Dictionary<string, DialerProfile>(StringComparer.Ordinal);

        foreach (var profileId in waitingItems.Select(item => item.DialerProfileId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            stampProfiles[profileId] = await _classifier.ReadProfileAsync(profileId, cancellationToken);
        }

        diagnostics.ProfileStamps = DispatchQueueLoadSummary.Stamps(waitingItems, id => stampProfiles.GetValueOrDefault(id));

        var head = waitingItems.Count == 0
            ? null
            : await _queueItemManager.FindNextWaitingAsync(CampaignRoutingQueue.Create(queueId), now, cancellationToken);

        diagnostics.Pacing = DispatchQueueLoadSummary.Pacing(
            head,
            head is null ? null : await _classifier.ReadProfileAsync(head.DialerProfileId, cancellationToken));

        if (diagnostics.PendingCount == 0)
        {
            return diagnostics;
        }

        var records = (await pending.OrderBy(index => index.ScheduledUtc).Take(MaxEvaluatedRecords).ListAsync(cancellationToken)).ToArray();

        var waiting = waitingItems
            .Where(item => !string.IsNullOrEmpty(item.ActivityItemId))
            .GroupBy(item => item.ActivityItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var pendingRecords = records
            .Select(index => new PendingRecord(index.ItemId, index.Source, index.InteractionType.ToString(), index.Channel, index.DialerProfileId))
            .ToArray();

        diagnostics.ActivityMix = DispatchQueueLoadSummary.Mix(pendingRecords);
        (diagnostics.ProfileMismatchCount, diagnostics.ActivitiesWithoutProfileCount) = DispatchQueueLoadSummary.Compare(waiting, pendingRecords);

        var verdicts = await _classifier.ClassifyAsync(
            pendingRecords
                .Select(record => new DialerRecordInput(
                    record.ActivityId,
                    waiting.TryGetValue(record.ActivityId, out var queueItem),
                    queueItem?.DialerProfileId,
                    record.DialerProfileId))
                .ToArray(),
            now,
            cancellationToken);

        var buckets = new Dictionary<DispatchBlocker, DispatchBucket>();

        foreach (var record in records)
        {
            if (!verdicts.TryGetValue(record.ItemId, out var verdict))
            {
                continue;
            }

            diagnostics.EvaluatedCount++;

            if (!buckets.TryGetValue(verdict.Blocker, out var bucket))
            {
                bucket = new DispatchBucket { Blocker = verdict.Blocker };
                buckets[verdict.Blocker] = bucket;
            }

            bucket.Count++;

            if (verdict.DueUtc is { } due && (bucket.NextDueUtc is null || due < bucket.NextDueUtc))
            {
                bucket.NextDueUtc = due;
            }
        }

        foreach (var bucket in buckets.Values
            .OrderBy(bucket => bucket.Blocker == DispatchBlocker.DialableNow ? 0 : 1)
            .ThenByDescending(bucket => bucket.Count))
        {
            diagnostics.Buckets.Add(bucket);
        }

        Tally(diagnostics);

        return diagnostics;
    }

    private async Task<int> CountItemsAsync(string queueId, QueueItemStatus status, CancellationToken cancellationToken)
        => await _session.QueryIndex<QueueItemIndex>(
            index => index.QueueId == queueId && index.Status == status,
            collection: ContactCenterStorage.CollectionName)
            .CountAsync(cancellationToken);

    internal static (DispatchBlocker Blocker, DateTime? DueUtc) Evaluate(
        QueueItem queueItem,
        DialerProfile profile,
        OmnichannelActivity activity,
        ContactCenterWorkState workState,
        Interaction lastInteraction,
        DialerSuppressionEventData suppression,
        DateTime now)
    {
        return Evaluate(queueItem is not null, queueItem?.DialerProfileId, profile, activity, workState, lastInteraction, suppression, now);
    }

    /// <summary>
    /// Decides what holds one pending campaign record back, if anything.
    /// </summary>
    /// <param name="queued">Whether the record has an item in its campaign's queue.</param>
    /// <param name="profileId">The dialer profile the record is dialed under, if any.</param>
    /// <param name="profile">That profile, or <see langword="null"/> when it is not set, is the built-in queued-callback
    /// profile, or no longer exists.</param>
    /// <param name="activity">The record.</param>
    /// <param name="workState">The record's work state, which holds its attempt count.</param>
    /// <param name="lastInteraction">The record's last call, if any.</param>
    /// <param name="suppression">The compliance gate's latest suppression of the record that no call has followed.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The reason the record is held back and when it becomes due, or <see cref="DispatchBlocker.DialableNow"/>.</returns>
    internal static (DispatchBlocker Blocker, DateTime? DueUtc) Evaluate(
        bool queued,
        string profileId,
        DialerProfile profile,
        OmnichannelActivity activity,
        ContactCenterWorkState workState,
        Interaction lastInteraction,
        DialerSuppressionEventData suppression,
        DateTime now)
    {
        // Routing only sees what waits in the campaign's queue.
        if (!queued)
        {
            return (DispatchBlocker.NotQueued, null);
        }

        // A record stamped with a profile that has since been deleted is never paced: the pacer reads the stamped profile
        // and finds nothing to pace with.
        if (profile is null && !string.IsNullOrEmpty(profileId) && !QueueCallbackDialerProfile.IsCallbackProfile(profileId))
        {
            return (DispatchBlocker.ProfileMissing, null);
        }

        // The queue gate lets a record with no profile, or a queued callback, through to routing untouched.
        if (profile is not null)
        {
            if (DialerWorkHoldEvaluator.EvaluateProfile(profile) == DispatchBlocker.ProfileDisabled)
            {
                return (DispatchBlocker.ProfileDisabled, null);
            }

            var lastOutcome = lastInteraction is null ? null : DialerCallMetadata.GetOutcome(lastInteraction);
            var hold = DialerWorkHoldEvaluator.EvaluateRecord(
                profile,
                activity,
                ContactCenterWorkState.NextAttemptNumber(workState, activity.Attempts),
                lastInteraction?.EndedUtc,
                string.Equals(lastOutcome, DialerAttemptOutcomes.Answered, StringComparison.Ordinal),
                now);

            if (hold.Blocker != DispatchBlocker.DialableNow)
            {
                return hold;
            }
        }

        // Past the queue gate, the compliance gate decides at dial time. Its latest word on the record stands unless a call
        // was placed after it.
        if (suppression is not null && ToBlocker(suppression.Reason) is { } suppressed)
        {
            return (suppressed, null);
        }

        return (DispatchBlocker.DialableNow, null);
    }

    private static DispatchBlocker? ToBlocker(DialerSuppressionReason reason)
        => reason switch
        {
            DialerSuppressionReason.NoDestination => DispatchBlocker.NoDestination,
            DialerSuppressionReason.MaxAttemptsReached => DispatchBlocker.AttemptsExhausted,
            DialerSuppressionReason.RetryCoolDown => DispatchBlocker.RetryCoolDown,
            DialerSuppressionReason.DoNotCall => DispatchBlocker.DoNotCall,
            DialerSuppressionReason.NationalDoNotCallRegistry => DispatchBlocker.NationalDoNotCallRegistry,
            DialerSuppressionReason.OutsideCallingWindow => DispatchBlocker.OutsideCallingWindow,
            DialerSuppressionReason.AbandonmentRateExceeded => DispatchBlocker.AbandonmentRateExceeded,
            DialerSuppressionReason.ComplianceScreeningUnavailable => DispatchBlocker.ComplianceScreeningUnavailable,
            DialerSuppressionReason.NumberNotInService => DispatchBlocker.NumberNotInService,
            _ => null,
        };

    private static void Tally(QueueDispatchDiagnostics diagnostics)
    {
        diagnostics.DialableNowCount = diagnostics.Buckets
            .Where(bucket => bucket.Blocker == DispatchBlocker.DialableNow)
            .Sum(bucket => bucket.Count);
        diagnostics.HeldBackCount = diagnostics.Buckets
            .Where(bucket => bucket.Blocker != DispatchBlocker.DialableNow)
            .Sum(bucket => bucket.Count);
    }

    // The latest routing decision of each queue, when it found nobody to take the work.
    private async Task<Dictionary<string, DispatchRoutingFinding>> ReadRoutingFindingsAsync(string[] queueIds, DateTime now, CancellationToken cancellationToken)
    {
        var wanted = new HashSet<string>(queueIds, StringComparer.OrdinalIgnoreCase);
        var findings = new Dictionary<string, DispatchRoutingFinding>(StringComparer.OrdinalIgnoreCase);
        var events = await _eventStore.GetByAggregateWindowAsync(
            nameof(QueueItem),
            [ContactCenterConstants.Events.RoutingDecisionMade],
            aggregateIds: null,
            now - RoutingLookback,
            now,
            cancellationToken);

        // Oldest first, so the last decision of a queue is the one left standing: a success clears an earlier failure.
        foreach (var interactionEvent in events)
        {
            var data = DialerRecordClassifier.ReadData<ActivityRoutingDecisionEventData>(interactionEvent);

            if (data is null || string.IsNullOrEmpty(data.QueueId) || !wanted.Contains(data.QueueId))
            {
                continue;
            }

            if (data.Succeeded)
            {
                findings.Remove(data.QueueId);

                continue;
            }

            findings[data.QueueId] = new DispatchRoutingFinding
            {
                OccurredUtc = interactionEvent.OccurredUtc,
                Reason = data.Reason,
                CandidateCount = data.Candidates.Count,
                CandidateReasons = data.Candidates
                    .Where(candidate => !candidate.IsEligible)
                    .SelectMany(candidate => candidate.Reasons.Distinct(StringComparer.Ordinal))
                    .GroupBy(reason => reason, StringComparer.Ordinal)
                    .Select(group => new DispatchCandidateReason { Reason = group.Key, AgentCount = group.Count() })
                    .OrderByDescending(reason => reason.AgentCount)
                    .ThenBy(reason => reason.Reason, StringComparer.Ordinal)
                    .ToList(),
            };
        }

        return findings;
    }
}
