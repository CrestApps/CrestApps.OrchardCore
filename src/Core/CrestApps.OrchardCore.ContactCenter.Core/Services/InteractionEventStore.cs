using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IInteractionEventStore"/>.
/// </summary>
public sealed class InteractionEventStore : DocumentCatalog<InteractionEvent, InteractionEventIndex>, IInteractionEventStore
{
    private const int AggregateIdBatchSize = 500;

    private readonly IInteractionEventUpcastService _upcastService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="upcastService">The service that brings a stored event to the current schema version.</param>
    public InteractionEventStore(
        ISession session,
        IInteractionEventUpcastService upcastService)
        : base(session)
    {
        _upcastService = upcastService;
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <summary>
    /// Brings every event read through this store to the current schema version. The conversion belongs here
    /// rather than at each caller because the durable event log is read from several places — post-commit
    /// dispatch, outbox redelivery, projection replay and reporting — and a caller that forgot to convert would
    /// not fail, it would read a stale payload as though it were current.
    /// </summary>
    /// <param name="record">The event read from storage.</param>
    /// <returns>A completed task.</returns>
    protected override ValueTask LoadingAsync(InteractionEvent record)
    {
        _upcastService.Upcast(record);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InteractionEvent>> GetByInteractionAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        var events = await Session.Query<InteractionEvent, InteractionEventIndex>(
            index => index.InteractionId == interactionId,
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.OccurredUtc)
            .ListAsync(cancellationToken);

        return await LoadedAsync(events);
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        var match = await Session.Query<InteractionEvent, InteractionEventIndex>(
            index => index.IdempotencyKey == idempotencyKey,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        return match is not null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InteractionEvent>> GetOlderThanAsync(DateTime cutoffUtc, int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? 100 : maxCount;

        var events = await Session.Query<InteractionEvent, InteractionEventIndex>(
            index => index.OccurredUtc < cutoffUtc,
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.OccurredUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return await LoadedAsync(events);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InteractionEvent>> GetByAggregateTypeAsync(
        string aggregateType,
        IEnumerable<string> eventTypes,
        DateTime occurredThroughUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(aggregateType);

        var types = eventTypes is null
            ? []
            : eventTypes.Where(eventType => !string.IsNullOrEmpty(eventType)).ToArray();

        var query = Session.Query<InteractionEvent, InteractionEventIndex>(
            index => index.AggregateType == aggregateType && index.OccurredUtc <= occurredThroughUtc,
            collection: ContactCenterStorage.CollectionName);

        if (types.Length > 0)
        {
            query = query.Where(index => index.EventType.IsIn(types));
        }

        var events = await query
            .OrderBy(index => index.OccurredUtc)
            .ThenBy(index => index.ItemId)
            .ListAsync(cancellationToken);

        return await LoadedAsync(events);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InteractionEvent>> GetByAggregateWindowAsync(
        string aggregateType,
        IEnumerable<string> eventTypes,
        IEnumerable<string> aggregateIds,
        DateTime fromUtc,
        DateTime throughUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(aggregateType);

        var types = Distinct(eventTypes);

        if (aggregateIds is null)
        {
            return await LoadedAsync(await QueryWindow(aggregateType, types, ids: null, fromUtc, throughUtc).ListAsync(cancellationToken));
        }

        var events = new List<InteractionEvent>();

        // The identifiers are sent in bounded batches, so a tenant with thousands of agents stays well inside every
        // database's limit on the parameters of one statement.
        foreach (var batch in Distinct(aggregateIds).Chunk(AggregateIdBatchSize))
        {
            events.AddRange(await QueryWindow(aggregateType, types, batch, fromUtc, throughUtc).ListAsync(cancellationToken));
        }

        return await LoadedAsync(events
            .OrderBy(interactionEvent => interactionEvent.OccurredUtc)
            .ThenBy(interactionEvent => interactionEvent.ItemId, StringComparer.Ordinal));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InteractionEvent>> GetLatestBeforeAsync(
        string aggregateType,
        IEnumerable<string> eventTypes,
        IEnumerable<string> aggregateIds,
        DateTime beforeUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(aggregateType);

        if (aggregateIds is null)
        {
            return [];
        }

        var types = Distinct(eventTypes);
        var events = new List<InteractionEvent>();

        // One seek per aggregate on the aggregate index, newest first: the cost is the number of aggregates, never
        // the length of their history.
        foreach (var aggregateId in Distinct(aggregateIds))
        {
            var query = Session.Query<InteractionEvent, InteractionEventIndex>(
                index => index.AggregateType == aggregateType && index.AggregateId == aggregateId && index.OccurredUtc < beforeUtc,
                collection: ContactCenterStorage.CollectionName);

            if (types.Length > 0)
            {
                query = query.Where(index => index.EventType.IsIn(types));
            }

            var latest = await query
                .OrderByDescending(index => index.OccurredUtc)
                .ThenByDescending(index => index.DocumentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (latest is not null)
            {
                events.Add(latest);
            }
        }

        return await LoadedAsync(events
            .OrderBy(interactionEvent => interactionEvent.OccurredUtc)
            .ThenBy(interactionEvent => interactionEvent.ItemId, StringComparer.Ordinal));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InteractionEvent>> GetOrderedPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var boundedSkip = skip < 0 ? 0 : skip;
        var boundedTake = take <= 0 ? 100 : take;

        var events = await Session.Query<InteractionEvent, InteractionEventIndex>(
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.OccurredUtc)
            .ThenBy(index => index.ItemId)
            .Skip(boundedSkip)
            .Take(boundedTake)
            .ListAsync(cancellationToken);

        return await LoadedAsync(events);
    }

    private IQuery<InteractionEvent, InteractionEventIndex> QueryWindow(
        string aggregateType,
        string[] types,
        string[] ids,
        DateTime fromUtc,
        DateTime throughUtc)
    {
        var query = Session.Query<InteractionEvent, InteractionEventIndex>(
            index => index.AggregateType == aggregateType && index.OccurredUtc >= fromUtc && index.OccurredUtc <= throughUtc,
            collection: ContactCenterStorage.CollectionName);

        if (ids is not null)
        {
            query = query.Where(index => index.AggregateId.IsIn(ids));
        }

        if (types.Length > 0)
        {
            query = query.Where(index => index.EventType.IsIn(types));
        }

        return query
            .OrderBy(index => index.OccurredUtc)
            .ThenBy(index => index.ItemId);
    }

    private static string[] Distinct(IEnumerable<string> values)
        => values is null
            ? []
            : values.Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).ToArray();
}
