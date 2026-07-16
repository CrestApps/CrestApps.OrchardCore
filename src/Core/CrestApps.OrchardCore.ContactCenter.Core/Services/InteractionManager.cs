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
    public async Task<Interaction> FindByProviderInteractionIdAsync(string providerInteractionId, CancellationToken cancellationToken = default)
    {
        var interaction = await _store.FindByProviderInteractionIdAsync(providerInteractionId, cancellationToken);

        if (interaction is not null)
        {
            await LoadAsync(interaction, cancellationToken);
        }

        return interaction;
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindByProviderInteractionIdAsync(
        string providerName,
        string providerInteractionId,
        CancellationToken cancellationToken = default)
    {
        var interaction = await _store.FindByProviderInteractionIdAsync(providerName, providerInteractionId, cancellationToken);

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

    /// <inheritdoc/>
    public Task<int> CountActiveByAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        return _store.CountActiveByAgentAsync(agentId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, int>> CountActiveByAgentIdsAsync(
        IReadOnlyCollection<string> agentIds,
        CancellationToken cancellationToken = default)
    {
        return _store.CountActiveByAgentIdsAsync(agentIds, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindActiveByAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        var interaction = await _store.FindActiveByAgentAsync(agentId, cancellationToken);

        if (interaction is not null)
        {
            await LoadAsync(interaction, cancellationToken);
        }

        return interaction;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Interaction>> ListPendingWrapUpsByAgentAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        var interactions = await _store.ListPendingWrapUpsByAgentAsync(agentId, cancellationToken);

        foreach (var interaction in interactions)
        {
            await LoadAsync(interaction, cancellationToken);
        }

        return interactions;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Interaction>> ListRecentByAgentAsync(string agentId, int take, CancellationToken cancellationToken = default)
    {
        var interactions = await _store.ListRecentByAgentAsync(agentId, take, cancellationToken);

        foreach (var interaction in interactions)
        {
            await LoadAsync(interaction, cancellationToken);
        }

        return interactions;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Interaction>> ListActiveWithProviderCallIdAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var interactions = await _store.ListActiveWithProviderCallIdAsync(maxCount, cancellationToken);

        foreach (var interaction in interactions)
        {
            await LoadAsync(interaction, cancellationToken);
        }

        return interactions;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Interaction>> ListActiveWithProviderCallIdAsync(
        string providerName,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var interactions = await _store.ListActiveWithProviderCallIdAsync(providerName, maxCount, cancellationToken);

        foreach (var interaction in interactions)
        {
            await LoadAsync(interaction, cancellationToken);
        }

        return interactions;
    }
}
