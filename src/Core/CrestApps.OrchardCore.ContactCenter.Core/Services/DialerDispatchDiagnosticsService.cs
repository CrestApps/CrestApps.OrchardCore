using System.Text.Json;
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
using YesSql.Services;

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

    private const int BatchSize = 500;

    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IBusinessHoursService _businessHours;
    private readonly IDialerProfileReader _profileReader;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterWorkStateStore _workStateStore;
    private readonly IInteractionEventStore _eventStore;
    private readonly ISession _session;
    private readonly IClock _clock;

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
        _profileReader = profileReader;
        _activityManager = activityManager;
        _workStateStore = workStateStore;
        _eventStore = eventStore;
        _session = session;
        _clock = clock;
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

        if (diagnostics.PendingCount == 0)
        {
            return diagnostics;
        }

        var records = (await pending.OrderBy(index => index.ScheduledUtc).Take(MaxEvaluatedRecords).ListAsync(cancellationToken)).ToArray();
        var recordIds = records.Select(index => index.ItemId).ToArray();

        var waiting = (await _queueItemManager.GetWaitingAsync(queueId, cancellationToken))
            .Where(item => !string.IsNullOrEmpty(item.ActivityItemId))
            .GroupBy(item => item.ActivityItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var activities = await LoadActivitiesAsync(recordIds, cancellationToken);
        var workStates = await LoadWorkStatesAsync(recordIds, cancellationToken);
        var lastInteractions = await LoadLastInteractionsAsync(recordIds, cancellationToken);
        var suppressions = await LoadSuppressionsAsync(recordIds, now, cancellationToken);
        var profiles = new Dictionary<string, DialerProfile>(StringComparer.Ordinal);
        var buckets = new Dictionary<DispatchBlocker, DispatchBucket>();

        foreach (var record in records)
        {
            if (!activities.TryGetValue(record.ItemId, out var activity))
            {
                continue;
            }

            waiting.TryGetValue(record.ItemId, out var queueItem);
            workStates.TryGetValue(record.ItemId, out var workState);
            lastInteractions.TryGetValue(record.ItemId, out var lastInteraction);
            suppressions.TryGetValue(record.ItemId, out var suppression);

            var profileId = queueItem?.DialerProfileId ?? record.DialerProfileId;
            DialerProfile profile = null;

            if (!string.IsNullOrEmpty(profileId) && !QueueCallbackDialerProfile.IsCallbackProfile(profileId) && !profiles.TryGetValue(profileId, out profile))
            {
                profile = await _profileReader.FindByIdAsync(profileId, cancellationToken);
                profiles[profileId] = profile;
            }

            var (blocker, dueUtc) = Evaluate(queueItem, profile, activity, workState, lastInteraction, suppression, now);
            diagnostics.EvaluatedCount++;

            if (!buckets.TryGetValue(blocker, out var bucket))
            {
                bucket = new DispatchBucket { Blocker = blocker };
                buckets[blocker] = bucket;
            }

            bucket.Count++;

            if (dueUtc is { } due && (bucket.NextDueUtc is null || due < bucket.NextDueUtc))
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

    internal static (DispatchBlocker Blocker, DateTime? DueUtc) Evaluate(
        QueueItem queueItem,
        DialerProfile profile,
        OmnichannelActivity activity,
        ContactCenterWorkState workState,
        Interaction lastInteraction,
        DialerSuppressionEventData suppression,
        DateTime now)
    {
        // Routing only sees what waits in the campaign's queue.
        if (queueItem is null)
        {
            return (DispatchBlocker.NotQueued, null);
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

    private async Task<Dictionary<string, OmnichannelActivity>> LoadActivitiesAsync(string[] ids, CancellationToken cancellationToken)
    {
        var activities = new Dictionary<string, OmnichannelActivity>(StringComparer.Ordinal);

        foreach (var batch in ids.Chunk(BatchSize))
        {
            foreach (var activity in await _activityManager.GetByIdsAsync(batch, cancellationToken))
            {
                activities[activity.ItemId] = activity;
            }
        }

        return activities;
    }

    private async Task<Dictionary<string, ContactCenterWorkState>> LoadWorkStatesAsync(string[] ids, CancellationToken cancellationToken)
    {
        var states = new Dictionary<string, ContactCenterWorkState>(StringComparer.Ordinal);

        foreach (var batch in ids.Chunk(BatchSize))
        {
            foreach (var state in await _workStateStore.GetByActivityIdsAsync(batch, cancellationToken))
            {
                states[state.ActivityItemId] = state;
            }
        }

        return states;
    }

    private async Task<Dictionary<string, Interaction>> LoadLastInteractionsAsync(string[] ids, CancellationToken cancellationToken)
    {
        var last = new Dictionary<string, Interaction>(StringComparer.Ordinal);

        foreach (var batch in ids.Chunk(BatchSize))
        {
            var interactions = await _session.Query<Interaction, InteractionIndex>(
                index => index.ActivityItemId.IsIn(batch),
                collection: ContactCenterStorage.CollectionName)
                .ListAsync(cancellationToken);

            foreach (var interaction in interactions)
            {
                if (!string.IsNullOrEmpty(interaction.ActivityItemId) &&
                    (!last.TryGetValue(interaction.ActivityItemId, out var existing) || interaction.CreatedUtc > existing.CreatedUtc))
                {
                    last[interaction.ActivityItemId] = interaction;
                }
            }
        }

        return last;
    }

    // The latest suppression of each record that no call has followed.
    private async Task<Dictionary<string, DialerSuppressionEventData>> LoadSuppressionsAsync(string[] ids, DateTime now, CancellationToken cancellationToken)
    {
        var latest = new Dictionary<string, (DateTime OccurredUtc, DialerSuppressionEventData Data)>(StringComparer.Ordinal);
        var events = await _eventStore.GetByAggregateWindowAsync(
            nameof(OmnichannelActivity),
            [ContactCenterConstants.Events.DialSuppressed],
            ids,
            now - SuppressionLookback,
            now,
            cancellationToken);

        foreach (var interactionEvent in events)
        {
            var data = ReadData<DialerSuppressionEventData>(interactionEvent);

            if (data is null || string.IsNullOrEmpty(interactionEvent.AggregateId))
            {
                continue;
            }

            if (!latest.TryGetValue(interactionEvent.AggregateId, out var existing) || interactionEvent.OccurredUtc >= existing.OccurredUtc)
            {
                latest[interactionEvent.AggregateId] = (interactionEvent.OccurredUtc, data);
            }
        }

        if (latest.Count == 0)
        {
            return [];
        }

        // A call placed after the suppression means the record was dialed since; the suppression no longer explains it.
        var lastInteractions = await LoadLastInteractionsAsync(latest.Keys.ToArray(), cancellationToken);
        var current = new Dictionary<string, DialerSuppressionEventData>(StringComparer.Ordinal);

        foreach (var (activityId, entry) in latest)
        {
            if (!lastInteractions.TryGetValue(activityId, out var interaction) || interaction.CreatedUtc <= entry.OccurredUtc)
            {
                current[activityId] = entry.Data;
            }
        }

        return current;
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
            var data = ReadData<ActivityRoutingDecisionEventData>(interactionEvent);

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

    private static T ReadData<T>(InteractionEvent interactionEvent)
        where T : class
    {
        try
        {
            return interactionEvent.GetData<T>();
        }
        catch (JsonException)
        {
            // A payload written by a release this one cannot read says nothing about now.
            return null;
        }
    }
}
