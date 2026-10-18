using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IDialerDecisionLog"/>, which reads the decision events through the
/// <see cref="DialerDecisionIndex"/> and the <see cref="DialerDecisionAgentIndex"/>.
/// </summary>
internal sealed class DialerDecisionLog : IDialerDecisionLog
{
    private const int QueueIdBatchSize = 500;

    private readonly ISession _session;
    private readonly IInteractionEventUpcastService _upcastService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerDecisionLog"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="upcastService">The service that brings a stored event to the current schema version.</param>
    public DialerDecisionLog(
        ISession session,
        IInteractionEventUpcastService upcastService)
    {
        _session = session;
        _upcastService = upcastService;
    }

    /// <inheritdoc/>
    public async Task<bool> TryCoalesceRoutingDecisionAsync(
        ActivityRoutingDecisionEventData decision,
        DateTime decidedUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.Succeeded || string.IsNullOrEmpty(decision.QueueId))
        {
            return false;
        }

        var queueId = decision.QueueId;

        // Assignment holds the queue's lock while it decides, so the latest decision cannot change under this read.
        var latest = await _session.Query<InteractionEvent, DialerDecisionIndex>(
            index => index.EventType == ContactCenterConstants.Events.RoutingDecisionMade && index.QueueId == queueId,
            collection: ContactCenterStorage.CollectionName)
            .OrderByDescending(index => index.LastSeenUtc)
            .ThenByDescending(index => index.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return false;
        }

        _upcastService.Upcast(latest);

        var previous = ReadData(latest);

        if (!RoutingDecisionCoalescing.IsRepeat(latest, previous, decision, decidedUtc))
        {
            return false;
        }

        var lastSeenUtc = RoutingDecisionCoalescing.GetLastSeenUtc(latest, previous);

        previous.OccurrenceCount = RoutingDecisionCoalescing.GetOccurrenceCount(previous) + 1;
        previous.LastSeenUtc = decidedUtc > lastSeenUtc
            ? decidedUtc
            : lastSeenUtc;

        latest.SetData(previous);

        await _session.SaveAsync(latest, false, ContactCenterStorage.CollectionName, cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InteractionEvent>> GetRoutingDecisionsSeenSinceAsync(
        IReadOnlyCollection<string> queueIds,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var ids = Distinct(queueIds);

        if (ids.Length == 0)
        {
            return [];
        }

        var events = new List<InteractionEvent>();

        foreach (var batch in ids.Chunk(QueueIdBatchSize))
        {
            events.AddRange(await _session.Query<InteractionEvent, DialerDecisionIndex>(
                index => index.EventType == ContactCenterConstants.Events.RoutingDecisionMade &&
                    index.LastSeenUtc >= sinceUtc &&
                    index.QueueId.IsIn(batch),
                collection: ContactCenterStorage.CollectionName)
                .ListAsync(cancellationToken));
        }

        foreach (var interactionEvent in events)
        {
            _upcastService.Upcast(interactionEvent);
        }

        return events
            .OrderBy(interactionEvent => RoutingDecisionCoalescing.GetLastSeenUtc(interactionEvent, ReadData(interactionEvent)))
            .ThenBy(interactionEvent => interactionEvent.ItemId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InteractionEvent>> ListAsync(
        DialerDecisionLogQuery query,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var take = maxCount <= 0 ? 100 : maxCount;
        IEnumerable<InteractionEvent> events;

        if (!string.IsNullOrEmpty(query.AgentId))
        {
            events = await FilterAgents(_session.Query<InteractionEvent, DialerDecisionAgentIndex>(collection: ContactCenterStorage.CollectionName), query)
                .OrderByDescending(index => index.LastSeenUtc)
                .ThenByDescending(index => index.DocumentId)
                .Take(take)
                .ListAsync(cancellationToken);
        }
        else
        {
            events = await FilterDecisions(_session.Query<InteractionEvent, DialerDecisionIndex>(collection: ContactCenterStorage.CollectionName), query)
                .OrderByDescending(index => index.LastSeenUtc)
                .ThenByDescending(index => index.DocumentId)
                .Take(take)
                .ListAsync(cancellationToken);
        }

        var materialized = events.ToArray();

        foreach (var interactionEvent in materialized)
        {
            _upcastService.Upcast(interactionEvent);
        }

        return materialized;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DialerDecisionIndex>> GetEntriesAsync(
        DialerDecisionLogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrEmpty(query.AgentId))
        {
            return (await FilterDecisionEntries(_session.QueryIndex<DialerDecisionIndex>(collection: ContactCenterStorage.CollectionName), query)
                .ListAsync(cancellationToken))
                .ToArray();
        }

        var agentEntries = await FilterAgentEntries(_session.QueryIndex<DialerDecisionAgentIndex>(collection: ContactCenterStorage.CollectionName), query)
            .ListAsync(cancellationToken);

        return agentEntries
            .Select(entry => new DialerDecisionIndex
            {
                ItemId = entry.ItemId,
                DocumentId = entry.DocumentId,
                EventType = entry.EventType,
                QueueId = entry.QueueId,
                CampaignId = entry.CampaignId,
                ActivityItemId = entry.ActivityItemId,
                AgentId = entry.IsSelected ? entry.AgentId : null,
                Succeeded = entry.Succeeded,
                Reason = entry.Reason,
                OccurrenceCount = entry.OccurrenceCount,
                OccurredUtc = entry.OccurredUtc,
                LastSeenUtc = entry.LastSeenUtc,
            })
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, DateTime>> GetLastRoutingDecisionTimesAsync(
        IReadOnlyCollection<string> queueIds,
        CancellationToken cancellationToken = default)
    {
        var times = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        // One seek per queue on the queue index, newest first: the cost is the number of queues, never the length of
        // their history.
        foreach (var queueId in Distinct(queueIds))
        {
            var latest = await _session.QueryIndex<DialerDecisionIndex>(
                index => index.EventType == ContactCenterConstants.Events.RoutingDecisionMade && index.QueueId == queueId,
                collection: ContactCenterStorage.CollectionName)
                .OrderByDescending(index => index.LastSeenUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (latest is not null)
            {
                times[queueId] = DateTime.SpecifyKind(latest.LastSeenUtc, DateTimeKind.Utc);
            }
        }

        return times;
    }

    private static IQuery<InteractionEvent, DialerDecisionIndex> FilterDecisions(
        IQuery<InteractionEvent, DialerDecisionIndex> query,
        DialerDecisionLogQuery filter)
    {
        if (filter.FromUtc is { } fromUtc)
        {
            query = query.Where(index => index.LastSeenUtc >= fromUtc);
        }

        if (filter.ToUtc is { } toUtc)
        {
            query = query.Where(index => index.OccurredUtc <= toUtc);
        }

        if (!string.IsNullOrEmpty(filter.EventType))
        {
            var eventType = filter.EventType;
            query = query.Where(index => index.EventType == eventType);
        }

        if (!string.IsNullOrEmpty(filter.QueueId))
        {
            var queueId = filter.QueueId;
            query = query.Where(index => index.QueueId == queueId);
        }

        if (!string.IsNullOrEmpty(filter.CampaignId))
        {
            var campaignId = filter.CampaignId;
            query = query.Where(index => index.CampaignId == campaignId);
        }

        if (!string.IsNullOrEmpty(filter.ActivityItemId))
        {
            var activityItemId = filter.ActivityItemId;
            query = query.Where(index => index.ActivityItemId == activityItemId);
        }

        if (!string.IsNullOrEmpty(filter.Reason))
        {
            var reason = filter.Reason;
            query = query.Where(index => index.Reason == reason);
        }

        return query;
    }

    private static IQuery<InteractionEvent, DialerDecisionAgentIndex> FilterAgents(
        IQuery<InteractionEvent, DialerDecisionAgentIndex> query,
        DialerDecisionLogQuery filter)
    {
        var agentId = filter.AgentId;
        query = query.Where(index => index.AgentId == agentId);

        if (filter.FromUtc is { } fromUtc)
        {
            query = query.Where(index => index.LastSeenUtc >= fromUtc);
        }

        if (filter.ToUtc is { } toUtc)
        {
            query = query.Where(index => index.OccurredUtc <= toUtc);
        }

        if (!string.IsNullOrEmpty(filter.EventType))
        {
            var eventType = filter.EventType;
            query = query.Where(index => index.EventType == eventType);
        }

        if (!string.IsNullOrEmpty(filter.QueueId))
        {
            var queueId = filter.QueueId;
            query = query.Where(index => index.QueueId == queueId);
        }

        if (!string.IsNullOrEmpty(filter.CampaignId))
        {
            var campaignId = filter.CampaignId;
            query = query.Where(index => index.CampaignId == campaignId);
        }

        if (!string.IsNullOrEmpty(filter.ActivityItemId))
        {
            var activityItemId = filter.ActivityItemId;
            query = query.Where(index => index.ActivityItemId == activityItemId);
        }

        if (!string.IsNullOrEmpty(filter.Reason))
        {
            var reason = filter.Reason;
            query = query.Where(index => index.Reason == reason);
        }

        return query;
    }

    private static IQueryIndex<DialerDecisionIndex> FilterDecisionEntries(
        IQueryIndex<DialerDecisionIndex> query,
        DialerDecisionLogQuery filter)
    {
        if (filter.FromUtc is { } fromUtc)
        {
            query = query.Where(index => index.LastSeenUtc >= fromUtc);
        }

        if (filter.ToUtc is { } toUtc)
        {
            query = query.Where(index => index.OccurredUtc <= toUtc);
        }

        if (!string.IsNullOrEmpty(filter.EventType))
        {
            var eventType = filter.EventType;
            query = query.Where(index => index.EventType == eventType);
        }

        if (!string.IsNullOrEmpty(filter.QueueId))
        {
            var queueId = filter.QueueId;
            query = query.Where(index => index.QueueId == queueId);
        }

        if (!string.IsNullOrEmpty(filter.CampaignId))
        {
            var campaignId = filter.CampaignId;
            query = query.Where(index => index.CampaignId == campaignId);
        }

        if (!string.IsNullOrEmpty(filter.ActivityItemId))
        {
            var activityItemId = filter.ActivityItemId;
            query = query.Where(index => index.ActivityItemId == activityItemId);
        }

        if (!string.IsNullOrEmpty(filter.Reason))
        {
            var reason = filter.Reason;
            query = query.Where(index => index.Reason == reason);
        }

        return query;
    }

    private static IQueryIndex<DialerDecisionAgentIndex> FilterAgentEntries(
        IQueryIndex<DialerDecisionAgentIndex> query,
        DialerDecisionLogQuery filter)
    {
        var agentId = filter.AgentId;
        query = query.Where(index => index.AgentId == agentId);

        if (filter.FromUtc is { } fromUtc)
        {
            query = query.Where(index => index.LastSeenUtc >= fromUtc);
        }

        if (filter.ToUtc is { } toUtc)
        {
            query = query.Where(index => index.OccurredUtc <= toUtc);
        }

        if (!string.IsNullOrEmpty(filter.EventType))
        {
            var eventType = filter.EventType;
            query = query.Where(index => index.EventType == eventType);
        }

        if (!string.IsNullOrEmpty(filter.QueueId))
        {
            var queueId = filter.QueueId;
            query = query.Where(index => index.QueueId == queueId);
        }

        if (!string.IsNullOrEmpty(filter.CampaignId))
        {
            var campaignId = filter.CampaignId;
            query = query.Where(index => index.CampaignId == campaignId);
        }

        if (!string.IsNullOrEmpty(filter.ActivityItemId))
        {
            var activityItemId = filter.ActivityItemId;
            query = query.Where(index => index.ActivityItemId == activityItemId);
        }

        if (!string.IsNullOrEmpty(filter.Reason))
        {
            var reason = filter.Reason;
            query = query.Where(index => index.Reason == reason);
        }

        return query;
    }

    private static ActivityRoutingDecisionEventData ReadData(InteractionEvent interactionEvent)
    {
        try
        {
            return interactionEvent.GetData<ActivityRoutingDecisionEventData>();
        }
        catch (JsonException)
        {
            // A payload this release cannot read is never extended; the next decision starts a new event.
            return null;
        }
    }

    private static string[] Distinct(IEnumerable<string> values)
        => values is null
            ? []
            : values.Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).ToArray();
}
