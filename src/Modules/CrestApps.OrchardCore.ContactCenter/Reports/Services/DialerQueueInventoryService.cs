using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// The default <see cref="IDialerQueueInventoryService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Queue items and pending activities are read from their indexes only: the dialer profile an item is stamped with is
/// indexed, so grouping a large dialer load by profile never reads an item's document. Only the records being explained
/// are read in full, by <see cref="DialerRecordClassifier"/>, which is what the live dashboard uses too, and at most
/// <see cref="DialerDispatchDiagnosticsService.MaxEvaluatedRecords"/> of each campaign, the earliest due first; the rest are
/// counted as pending but not explained.
/// </para>
/// <para>
/// A pending record is in its campaign's queue when an item for it is waiting, reserved or assigned there. A record with
/// no item there is held back as not queued, and is also counted as in no queue at all when no queue holds an item for it.
/// </para>
/// </remarks>
internal sealed class DialerQueueInventoryService : IDialerQueueInventoryService
{
    private const int BatchSize = 500;

    private readonly ISession _session;
    private readonly IQueueItemManager _queueItemManager;
    private readonly DialerRecordClassifier _classifier;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerQueueInventoryService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the queue item, activity and interaction indexes are read through.</param>
    /// <param name="queueItemManager">The queue items, read for the item at the head of each campaign queue.</param>
    /// <param name="classifier">The classifier that decides what holds each pending record back.</param>
    /// <param name="clock">The clock.</param>
    public DialerQueueInventoryService(
        ISession session,
        IQueueItemManager queueItemManager,
        DialerRecordClassifier classifier,
        IClock clock)
    {
        _session = session;
        _queueItemManager = queueItemManager;
        _classifier = classifier;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<DialerQueueInventory> GetInventoryAsync(DialerQueueInventoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.UtcNow;
        var campaignFilter = request.CampaignIds is null
            ? null
            : request.CampaignIds.Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);

        var pending = (request.PendingActivities ?? await ReadPendingAsync(campaignFilter, cancellationToken))
            .Where(activity => activity is not null &&
                !string.IsNullOrEmpty(activity.ItemId) &&
                !string.IsNullOrEmpty(activity.CampaignId) &&
                ActivityProgressTally.IsPending(activity.Status) &&
                (campaignFilter is null || campaignFilter.Contains(activity.CampaignId)))
            .DistinctBy(activity => activity.ItemId, StringComparer.Ordinal)
            .ToArray();

        var activeItems = (await _session.QueryIndex<QueueItemIndex>(
            index => index.Status == QueueItemStatus.Waiting ||
                index.Status == QueueItemStatus.Reserved ||
                index.Status == QueueItemStatus.Assigned,
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken))
            .ToArray();

        var itemsByActivity = activeItems
            .Where(item => !string.IsNullOrEmpty(item.ActivityItemId))
            .ToLookup(item => item.ActivityItemId, StringComparer.Ordinal);

        var rows = new Dictionary<RowKey, RowBuilder>();

        foreach (var item in activeItems)
        {
            var campaignId = ContactCenterConstants.CampaignQueue.GetCampaignId(item.QueueId);

            if (string.IsNullOrEmpty(campaignId) || (campaignFilter is not null && !campaignFilter.Contains(campaignId)))
            {
                continue;
            }

            var row = GetRow(rows, new RowKey(campaignId, item.QueueId, NormalizeProfileId(item.DialerProfileId)));

            switch (item.Status)
            {
                case QueueItemStatus.Waiting:
                    row.WaitingCount++;

                    if (row.OldestWaitingUtc is null || item.EnqueuedUtc < row.OldestWaitingUtc)
                    {
                        row.OldestWaitingUtc = item.EnqueuedUtc;
                    }

                    break;
                case QueueItemStatus.Reserved:
                    row.ReservedCount++;

                    break;
                default:
                    row.AssignedCount++;

                    break;
            }
        }

        // Each campaign's earliest-due records are explained; the rest are counted but not read in full.
        var placements = new List<(OmnichannelActivityIndex Activity, RowKey Key, bool InAnyQueue, bool Evaluated)>(pending.Length);
        var inputs = new List<DialerRecordInput>();

        foreach (var campaign in pending.GroupBy(activity => activity.CampaignId, StringComparer.Ordinal))
        {
            var queueId = ContactCenterConstants.CampaignQueue.CreateId(campaign.Key);
            var position = 0;

            foreach (var activity in campaign.OrderBy(activity => activity.ScheduledUtc).ThenBy(activity => activity.ItemId, StringComparer.Ordinal))
            {
                var items = itemsByActivity[activity.ItemId];
                var item = items.FirstOrDefault(candidate => string.Equals(candidate.QueueId, queueId, StringComparison.Ordinal));
                var evaluated = position++ < DialerDispatchDiagnosticsService.MaxEvaluatedRecords;
                var key = item is null
                    ? new RowKey(campaign.Key, null, null)
                    : new RowKey(campaign.Key, queueId, NormalizeProfileId(item.DialerProfileId));

                placements.Add((activity, key, items.Any(), evaluated));

                if (evaluated)
                {
                    inputs.Add(new DialerRecordInput(activity.ItemId, item is not null, item?.DialerProfileId, activity.DialerProfileId));
                }
            }
        }

        var verdicts = await _classifier.ClassifyAsync(inputs, now, cancellationToken);
        var campaigns = new Dictionary<string, CampaignBuilder>(StringComparer.Ordinal);

        foreach (var (activity, key, inAnyQueue, evaluated) in placements)
        {
            var row = GetRow(rows, key);
            var campaign = GetCampaign(campaigns, activity.CampaignId);

            row.Pending.Add(activity);
            campaign.Summary.PendingCount++;

            if (key.QueueId is not null && DispatchQueueLoadSummary.IsStampMismatch(key.StampedProfileId, activity.DialerProfileId))
            {
                row.ProfileMismatchCount++;
            }

            if (!inAnyQueue)
            {
                row.NotInAnyQueueCount++;
                campaign.Summary.NotInAnyQueueCount++;
            }

            if (!evaluated || !verdicts.TryGetValue(activity.ItemId, out var verdict))
            {
                row.NotEvaluatedCount++;
                campaign.Summary.NotEvaluatedCount++;

                continue;
            }

            AddToBucket(campaign.Buckets, verdict);

            if (verdict.Blocker == DispatchBlocker.DialableNow)
            {
                row.DialableNowCount++;
                campaign.Summary.DialableNowCount++;
            }
            else
            {
                row.HeldBackCount++;
                campaign.Summary.HeldBackCount++;
                AddToBucket(row.HeldBack, verdict);
            }
        }

        var inventory = new DialerQueueInventory
        {
            GeneratedUtc = now,
        };

        var queueFacts = new Dictionary<string, (DispatchPacing Pacing, DateTime? LastDialedUtc)>(StringComparer.Ordinal);

        foreach (var (key, builder) in rows
            .OrderBy(entry => entry.Key.CampaignId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.QueueId is null ? 1 : 0)
            .ThenByDescending(entry => entry.Value.WaitingCount)
            .ThenBy(entry => entry.Key.StampedProfileId, StringComparer.Ordinal))
        {
            var row = new DialerQueueInventoryRow
            {
                CampaignId = key.CampaignId,
                QueueId = key.QueueId,
                StampedProfileId = key.StampedProfileId,
                WaitingCount = builder.WaitingCount,
                ReservedCount = builder.ReservedCount,
                AssignedCount = builder.AssignedCount,
                OldestWaitingUtc = builder.OldestWaitingUtc,
                PendingCount = builder.Pending.Count,
                DialableNowCount = builder.DialableNowCount,
                HeldBackCount = builder.HeldBackCount,
                NotEvaluatedCount = builder.NotEvaluatedCount,
                ProfileMismatchCount = builder.ProfileMismatchCount,
                NotInAnyQueueCount = builder.NotInAnyQueueCount,
                HeldBackReasons = Order(builder.HeldBack.Values),
                ActivityMix = DispatchQueueLoadSummary.Mix(builder.Pending
                    .Select(activity => new PendingRecord(
                        activity.ItemId,
                        activity.Source,
                        activity.InteractionType.ToString(),
                        activity.Channel,
                        activity.DialerProfileId))),
            };

            if (key.StampedProfileId is not null)
            {
                if (QueueCallbackDialerProfile.IsCallbackProfile(key.StampedProfileId))
                {
                    row.StampedProfileIsCallback = true;
                }
                else if (await _classifier.ReadProfileAsync(key.StampedProfileId, cancellationToken) is { } stamped)
                {
                    row.StampedProfileName = stamped.Name;
                    row.StampedProfileMode = stamped.Mode.ToString();
                    row.StampedProfileEnabled = stamped.Enabled;
                }
                else
                {
                    row.StampedProfileMissing = true;
                }
            }

            foreach (var group in builder.Pending
                .GroupBy(activity => NormalizeProfileId(activity.DialerProfileId) ?? string.Empty, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal))
            {
                var profileId = group.Key.Length == 0 ? null : group.Key;

                row.ActivityProfiles.Add(new DialerInventoryProfileCount
                {
                    ProfileId = profileId,
                    ProfileName = (await _classifier.ReadProfileAsync(profileId, cancellationToken))?.Name,
                    Count = group.Count(),
                });
            }

            if (key.QueueId is not null)
            {
                if (!queueFacts.TryGetValue(key.QueueId, out var facts))
                {
                    facts = await ReadQueueFactsAsync(key.QueueId, rows, now, cancellationToken);
                    queueFacts[key.QueueId] = facts;
                }

                row.Pacing = facts.Pacing;
                row.LastDialedUtc = facts.LastDialedUtc;
            }

            inventory.Rows.Add(row);
            GetCampaign(campaigns, key.CampaignId);
        }

        foreach (var campaign in campaigns.Values.OrderBy(campaign => campaign.Summary.CampaignId, StringComparer.Ordinal))
        {
            campaign.Summary.Buckets = Order(campaign.Buckets.Values);
            inventory.Campaigns.Add(campaign.Summary);
        }

        return inventory;
    }

    private async Task<IReadOnlyList<OmnichannelActivityIndex>> ReadPendingAsync(HashSet<string> campaignFilter, CancellationToken cancellationToken)
    {
        if (campaignFilter is null)
        {
            return (await _session.QueryIndex<OmnichannelActivityIndex>(
                index => index.CampaignId != null &&
                    (index.Status == ActivityStatus.NotStated || index.Status == ActivityStatus.Pending || index.Status == ActivityStatus.Scheduled),
                collection: OmnichannelConstants.CollectionName)
                .ListAsync(cancellationToken))
                .ToArray();
        }

        var pending = new List<OmnichannelActivityIndex>();

        foreach (var batch in campaignFilter.Chunk(BatchSize))
        {
            pending.AddRange(await _session.QueryIndex<OmnichannelActivityIndex>(
                index => index.CampaignId.IsIn(batch) &&
                    (index.Status == ActivityStatus.NotStated || index.Status == ActivityStatus.Pending || index.Status == ActivityStatus.Scheduled),
                collection: OmnichannelConstants.CollectionName)
                .ListAsync(cancellationToken));
        }

        return pending;
    }

    // How the queue is paced, from the item at its head as the pacer reads it, and when it last placed a call.
    private async Task<(DispatchPacing Pacing, DateTime? LastDialedUtc)> ReadQueueFactsAsync(
        string queueId,
        Dictionary<RowKey, RowBuilder> rows,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var waiting = rows
            .Where(entry => string.Equals(entry.Key.QueueId, queueId, StringComparison.Ordinal))
            .Sum(entry => entry.Value.WaitingCount);

        var head = waiting == 0
            ? null
            : await _queueItemManager.FindNextWaitingAsync(CampaignRoutingQueue.Create(queueId), now, cancellationToken);

        var pacing = DispatchQueueLoadSummary.Pacing(
            head,
            head is null ? null : await _classifier.ReadProfileAsync(head.DialerProfileId, cancellationToken));

        var lastCall = await _session.QueryIndex<InteractionIndex>(
            index => index.QueueId == queueId && index.Direction == InteractionDirection.Outbound,
            collection: ContactCenterStorage.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return (pacing, lastCall?.CreatedUtc);
    }

    private static string NormalizeProfileId(string profileId)
        => string.IsNullOrEmpty(profileId) ? null : profileId;

    private static RowBuilder GetRow(Dictionary<RowKey, RowBuilder> rows, RowKey key)
    {
        if (!rows.TryGetValue(key, out var row))
        {
            row = new RowBuilder();
            rows[key] = row;
        }

        return row;
    }

    private static CampaignBuilder GetCampaign(Dictionary<string, CampaignBuilder> campaigns, string campaignId)
    {
        if (!campaigns.TryGetValue(campaignId, out var campaign))
        {
            campaign = new CampaignBuilder(campaignId);
            campaigns[campaignId] = campaign;
        }

        return campaign;
    }

    private static void AddToBucket(Dictionary<DispatchBlocker, DispatchBucket> buckets, DialerRecordVerdict verdict)
    {
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

    private static List<DispatchBucket> Order(IEnumerable<DispatchBucket> buckets)
    {
        return buckets
            .OrderBy(bucket => bucket.Blocker == DispatchBlocker.DialableNow ? 0 : 1)
            .ThenByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Blocker)
            .ToList();
    }

    private readonly record struct RowKey(string CampaignId, string QueueId, string StampedProfileId);

    private sealed class RowBuilder
    {
        public int WaitingCount { get; set; }

        public int ReservedCount { get; set; }

        public int AssignedCount { get; set; }

        public DateTime? OldestWaitingUtc { get; set; }

        public List<OmnichannelActivityIndex> Pending { get; } = [];

        public int DialableNowCount { get; set; }

        public int HeldBackCount { get; set; }

        public int NotEvaluatedCount { get; set; }

        public int ProfileMismatchCount { get; set; }

        public int NotInAnyQueueCount { get; set; }

        public Dictionary<DispatchBlocker, DispatchBucket> HeldBack { get; } = [];
    }

    private sealed class CampaignBuilder
    {
        public CampaignBuilder(string campaignId)
        {
            Summary = new DialerCampaignDispatchSummary { CampaignId = campaignId };
        }

        public DialerCampaignDispatchSummary Summary { get; }

        public Dictionary<DispatchBlocker, DispatchBucket> Buckets { get; } = [];
    }
}
