using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IAgentProfileManager"/>.
/// </summary>
public sealed class AgentProfileManager : CatalogManager<AgentProfile>, IAgentProfileManager
{
    private readonly IAgentProfileStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentProfileManager"/> class.
    /// </summary>
    /// <param name="store">The underlying agent profile store.</param>
    /// <param name="handlers">The catalog entry handlers for agent profiles.</param>
    /// <param name="logger">The logger instance.</param>
    public AgentProfileManager(
        IAgentProfileStore store,
        IEnumerable<ICatalogEntryHandler<AgentProfile>> handlers,
        ILogger<CatalogManager<AgentProfile>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _store.FindByUserIdAsync(userId, cancellationToken);

        if (profile is not null)
        {
            await LoadAsync(profile, cancellationToken);
        }

        return profile;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentProfile>> ListAvailableForQueueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        var profiles = await _store.ListAvailableForQueueAsync(queueId, cancellationToken);

        foreach (var profile in profiles)
        {
            await LoadAsync(profile, cancellationToken);
        }

        return profiles;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentProfile>> ListByPresenceAsync(
        AgentPresenceStatus presenceStatus,
        CancellationToken cancellationToken = default)
    {
        var profiles = await _store.ListByPresenceAsync(presenceStatus, cancellationToken);

        foreach (var profile in profiles)
        {
            await LoadAsync(profile, cancellationToken);
        }

        return profiles;
    }
}
