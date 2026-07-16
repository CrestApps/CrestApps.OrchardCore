namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterRetentionService"/>.
/// </summary>
public sealed class ContactCenterRetentionService : IContactCenterRetentionService
{
    private const int BatchSize = 100;
    private const int MaxBatches = 100;

    private readonly IInteractionEventStore _eventStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRetentionService"/> class.
    /// </summary>
    /// <param name="eventStore">The interaction event store.</param>
    public ContactCenterRetentionService(IInteractionEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    /// <inheritdoc/>
    public async Task<int> PurgeInteractionEventsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var purged = 0;

        for (var batch = 0; batch < MaxBatches; batch++)
        {
            var expired = await _eventStore.ListOlderThanAsync(cutoffUtc, BatchSize, cancellationToken);

            if (expired.Count == 0)
            {
                break;
            }

            foreach (var interactionEvent in expired)
            {
                await _eventStore.DeleteAsync(interactionEvent, cancellationToken);
                purged++;
            }

            if (expired.Count < BatchSize)
            {
                break;
            }
        }

        return purged;
    }
}
