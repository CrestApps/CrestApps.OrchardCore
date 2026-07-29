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
        CollectionName = ContactCenterConstants.CollectionName;
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
    public async Task<IReadOnlyList<InteractionEvent>> ListByInteractionAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        var events = await Session.Query<InteractionEvent, InteractionEventIndex>(
            index => index.InteractionId == interactionId,
            collection: ContactCenterConstants.CollectionName)
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
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        return match is not null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InteractionEvent>> ListOlderThanAsync(DateTime cutoffUtc, int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? 100 : maxCount;

        var events = await Session.Query<InteractionEvent, InteractionEventIndex>(
            index => index.OccurredUtc < cutoffUtc,
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.OccurredUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return await LoadedAsync(events);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InteractionEvent>> ListByAggregateTypeAsync(
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
            collection: ContactCenterConstants.CollectionName);

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
    public async Task<IReadOnlyList<InteractionEvent>> ListOrderedPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var boundedSkip = skip < 0 ? 0 : skip;
        var boundedTake = take <= 0 ? 100 : take;

        var events = await Session.Query<InteractionEvent, InteractionEventIndex>(
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.OccurredUtc)
            .ThenBy(index => index.ItemId)
            .Skip(boundedSkip)
            .Take(boundedTake)
            .ListAsync(cancellationToken);

        return await LoadedAsync(events);
    }
}
