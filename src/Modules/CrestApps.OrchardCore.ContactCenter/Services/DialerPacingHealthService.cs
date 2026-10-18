using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialerPacingHealthService"/>.
/// </summary>
/// <remarks>
/// A campaign queue counts when a record waits in it under an enabled dialer profile the system paces (Power,
/// Progressive or Predictive). A queue whose head record has no profile, or one that is turned off or that agents dial,
/// is not the dialer's to pace, so it is left out rather than reported as stalled.
/// </remarks>
public sealed class DialerPacingHealthService : IDialerPacingHealthService
{
    // Every outcome but a missed lock: the latest of these is the last time a cycle of the queue got past its pacing lock,
    // or had no need to take it.
    private static readonly DialerPacingCycleOutcome[] _notLockMissOutcomes = Enum.GetValues<DialerPacingCycleOutcome>()
        .Where(outcome => outcome != DialerPacingCycleOutcome.LockNotAcquired)
        .ToArray();

    private readonly IQueueItemStore _queueItemStore;
    private readonly IDialerProfileReader _profileReader;
    private readonly IDialerPacingCycleStore _cycleStore;
    private readonly IClock _clock;
    private readonly ContactCenterDialerPacingOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacingHealthService"/> class.
    /// </summary>
    /// <param name="queueItemStore">The queue items, which say which campaigns have work waiting.</param>
    /// <param name="profileReader">The reader of the dialer profile a campaign's waiting work was loaded under.</param>
    /// <param name="cycleStore">The record of each pacing cycle.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The dialer pacing options, which set the stall threshold.</param>
    public DialerPacingHealthService(
        IQueueItemStore queueItemStore,
        IDialerProfileReader profileReader,
        IDialerPacingCycleStore cycleStore,
        IClock clock,
        IOptions<ContactCenterDialerPacingOptions> options)
    {
        _queueItemStore = queueItemStore;
        _profileReader = profileReader;
        _cycleStore = cycleStore;
        _clock = clock;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<DialerPacingHealthSnapshot> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var snapshot = new DialerPacingHealthSnapshot
        {
            EvaluatedUtc = now,
            StalledAfter = _options.StalledAfter,
        };

        var campaignQueueIds = (await _queueItemStore.GetWaitingQueueIdsAsync(cancellationToken))
            .Where(ContactCenterConstants.IsCampaignQueue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (campaignQueueIds.Length == 0)
        {
            return snapshot;
        }

        var heads = await _queueItemStore.GetHeadWaitingByQueueAsync(campaignQueueIds, cancellationToken);
        var waitingCounts = await _queueItemStore.CountWaitingByQueueIdsAsync(campaignQueueIds, cancellationToken);
        var profiles = new Dictionary<string, DialerProfile>(StringComparer.Ordinal);
        var lockStarvationThreshold = DialerPacingScheduler.GetLockStarvationThreshold(_options);

        foreach (var head in heads)
        {
            if (head is null || string.IsNullOrEmpty(head.QueueId) || string.IsNullOrEmpty(head.DialerProfileId))
            {
                continue;
            }

            if (!profiles.TryGetValue(head.DialerProfileId, out var profile))
            {
                profile = await _profileReader.FindByIdAsync(head.DialerProfileId, cancellationToken);
                profiles[head.DialerProfileId] = profile;
            }

            if (profile is null || !profile.Enabled || !profile.Mode.IsAutomated())
            {
                continue;
            }

            var longestWaiting = await _queueItemStore.FindLongestWaitingAsync(head.QueueId, cancellationToken);
            var lastCycle = await _cycleStore.FindLatestByQueueAsync(head.QueueId, outcomes: null, cancellationToken);
            var lastCompleted = lastCycle is not null && DialerPacingCycleClassifier.IsCompleted(lastCycle.Outcome)
                ? lastCycle
                : await _cycleStore.FindLatestByQueueAsync(head.QueueId, DialerPacingCycleClassifier.CompletedOutcomes, cancellationToken);

            var waitingSinceUtc = longestWaiting?.EnqueuedUtc ?? head.EnqueuedUtc;
            var (state, silence) = DialerPacingHealthEvaluator.Evaluate(now, _options.StalledAfter, waitingSinceUtc, lastCompleted);

            // A queue whose cycles all skip themselves for want of its pacing lock is named for that, whatever the last
            // cycle that did run decided: that decision is as old as the lock trouble.
            DateTime? lockNotAcquiredSinceUtc = null;

            if (lastCycle?.Outcome == DialerPacingCycleOutcome.LockNotAcquired)
            {
                var lastNotLockMiss = await _cycleStore.FindLatestByQueueAsync(head.QueueId, _notLockMissOutcomes, cancellationToken);

                lockNotAcquiredSinceUtc = DialerPacingHealthEvaluator.FindLockNotAcquiredSince(now, lockStarvationThreshold, lastCycle, lastNotLockMiss);

                if (lockNotAcquiredSinceUtc is not null)
                {
                    state = DialerPacingHealthState.LockNotAcquired;
                }
            }

            snapshot.Queues.Add(new DialerPacingQueueHealth
            {
                QueueId = head.QueueId,
                CampaignId = ContactCenterConstants.CampaignQueue.GetCampaignId(head.QueueId),
                DialerProfileId = head.DialerProfileId,
                State = state,
                WaitingCount = waitingCounts.TryGetValue(head.QueueId, out var count) ? count : 0,
                WaitingSinceUtc = waitingSinceUtc,
                LastCycle = lastCycle,
                LastCompletedCycle = lastCompleted,
                TimeWithoutCompletedCycle = silence,
                LockNotAcquiredSinceUtc = lockNotAcquiredSinceUtc,
            });
        }

        snapshot.Queues = snapshot.Queues
            .OrderByDescending(queue => DialerPacingHealthEvaluator.Severity(queue.State))
            .ThenByDescending(queue => queue.TimeWithoutCompletedCycle)
            .ThenBy(queue => queue.QueueId, StringComparer.Ordinal)
            .ToList();

        return snapshot;
    }
}
