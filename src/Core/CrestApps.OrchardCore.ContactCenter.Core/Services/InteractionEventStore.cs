using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IInteractionEventStore"/>.
/// </summary>
public sealed class InteractionEventStore : DocumentCatalog<InteractionEvent, InteractionEventIndex>, IInteractionEventStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public InteractionEventStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
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

        return events.ToArray();
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
}
