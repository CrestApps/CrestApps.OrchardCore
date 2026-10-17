using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Dapper;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IInteractionEventStore"/>.
/// </summary>
public sealed class InteractionEventStore : DocumentCatalog<InteractionEvent, InteractionEventIndex>, IInteractionEventStore
{
    private const int AggregateIdBatchSize = 500;

    /// <summary>
    /// The most events one statement of a window read returns.
    /// </summary>
    internal const int WindowPageSize = 1000;

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
    /// <remarks>
    /// The window is read in pages of at most <see cref="WindowPageSize"/> events, each continuing after the last one
    /// read by occurrence time and document identifier, so no single statement holds a connection for the whole
    /// period however many events it contains. The cursor is taken from the index row itself rather than from the
    /// document, so it compares exactly at whatever precision the database stores the time with.
    /// </remarks>
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
        var events = new List<InteractionEvent>();

        if (aggregateIds is null)
        {
            await ReadWindowAsync(events, aggregateType, types, ids: null, fromUtc, throughUtc, cancellationToken);
        }
        else
        {
            // The identifiers are sent in bounded batches, so a tenant with thousands of agents stays well inside every
            // database's limit on the parameters of one statement.
            foreach (var batch in Distinct(aggregateIds).Chunk(AggregateIdBatchSize))
            {
                await ReadWindowAsync(events, aggregateType, types, batch, fromUtc, throughUtc, cancellationToken);
            }
        }

        return await LoadedAsync(events
            .OrderBy(interactionEvent => interactionEvent.OccurredUtc)
            .ThenBy(interactionEvent => interactionEvent.ItemId, StringComparer.Ordinal));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The aggregates are looked up in batches of one statement each, which seeks every aggregate's latest event on the
    /// aggregate index, rather than one round trip per aggregate. See <see cref="InteractionEventAnchorQuery"/>.
    /// </remarks>
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

        var ids = Distinct(aggregateIds);

        if (ids.Length == 0)
        {
            return [];
        }

        var types = Distinct(eventTypes);
        var documentIds = new List<long>();

        // The statement runs on the session's own transaction, so it first has to see what this unit of work wrote.
        await Session.FlushAsync(cancellationToken);
        var transaction = await Session.BeginTransactionAsync(cancellationToken);

        foreach (var batch in ids.Chunk(InteractionEventAnchorQuery.MaxAggregatesPerStatement))
        {
            var (sql, parameters) = InteractionEventAnchorQuery.Build(Session.Store.Configuration, aggregateType, types, batch, beforeUtc);

            documentIds.AddRange(await transaction.Connection.QueryAsync<long>(
                new CommandDefinition(
                    sql,
                    parameters,
                    transaction,
                    cancellationToken: cancellationToken)));
        }

        var events = new List<InteractionEvent>(documentIds.Count);

        foreach (var batch in documentIds.Distinct().Chunk(WindowPageSize))
        {
            events.AddRange(await Session.GetAsync<InteractionEvent>(batch, ContactCenterStorage.CollectionName, cancellationToken));
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

    private async Task ReadWindowAsync(
        List<InteractionEvent> events,
        string aggregateType,
        string[] types,
        string[] ids,
        DateTime fromUtc,
        DateTime throughUtc,
        CancellationToken cancellationToken)
    {
        DateTime? afterOccurredUtc = null;
        var afterDocumentId = 0L;

        while (true)
        {
            var query = Session.QueryIndex<InteractionEventIndex>(
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

            if (afterOccurredUtc.HasValue)
            {
                var cursorUtc = afterOccurredUtc.Value;
                var cursorDocumentId = afterDocumentId;

                query = query.Where(index => index.OccurredUtc > cursorUtc || (index.OccurredUtc == cursorUtc && index.DocumentId > cursorDocumentId));
            }

            var page = await query
                .OrderBy(index => index.OccurredUtc)
                .ThenBy(index => index.DocumentId)
                .Take(WindowPageSize)
                .ListAsync(cancellationToken);

            if (page.Count == 0)
            {
                return;
            }

            events.AddRange(await Session.GetAsync<InteractionEvent>(
                page.Select(row => row.DocumentId).ToArray(),
                ContactCenterStorage.CollectionName,
                cancellationToken));

            if (page.Count < WindowPageSize)
            {
                return;
            }

            var last = page[page.Count - 1];
            afterOccurredUtc = last.OccurredUtc;
            afterDocumentId = last.DocumentId;
        }
    }

    private static string[] Distinct(IEnumerable<string> values)
        => values is null
            ? []
            : values.Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).ToArray();
}
