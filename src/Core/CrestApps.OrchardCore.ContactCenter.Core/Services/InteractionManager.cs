using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IInteractionManager"/> that delegates storage
/// to <see cref="IInteractionStore"/> and loads entries through catalog handlers.
/// </summary>
public sealed class InteractionManager : CatalogManager<Interaction>, IInteractionManager
{
    private readonly IInteractionStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionManager"/> class.
    /// </summary>
    /// <param name="store">The underlying interaction store.</param>
    /// <param name="handlers">The catalog entry handlers for interactions.</param>
    /// <param name="logger">The logger instance.</param>
    public InteractionManager(
        IInteractionStore store,
        IEnumerable<ICatalogEntryHandler<Interaction>> handlers,
        ILogger<CatalogManager<Interaction>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        var interaction = await _store.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (interaction is not null)
        {
            await LoadAsync(interaction, cancellationToken);
        }

        return interaction;
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        var interaction = await _store.FindByCorrelationIdAsync(correlationId, cancellationToken);

        if (interaction is not null)
        {
            await LoadAsync(interaction, cancellationToken);
        }

        return interaction;
    }

    /// <inheritdoc/>
    public async Task<PageResult<Interaction>> PageByStatusAsync(int page, int pageSize, InteractionStatus status, CancellationToken cancellationToken = default)
    {
        var result = await _store.PageByStatusAsync(page, pageSize, status, cancellationToken);

        foreach (var entry in result.Entries)
        {
            await LoadAsync(entry, cancellationToken);
        }

        return result;
    }
}
