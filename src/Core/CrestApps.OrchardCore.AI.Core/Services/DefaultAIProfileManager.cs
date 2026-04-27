using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of <see cref="IAIProfileManager"/> that manages AI profile lifecycle
/// and supports querying profiles by type.
/// </summary>
public sealed class DefaultAIProfileManager : NamedCatalogManager<AIProfile>, IAIProfileManager
{
    private readonly IAIProfileStore _profileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIProfileManager"/> class.
    /// </summary>
    /// <param name="profileStore">The profile store for persistence and type-based queries.</param>
    /// <param name="handlers">The catalog entry handlers for profile lifecycle events.</param>
    /// <param name="logger">The logger instance.</param>
    public DefaultAIProfileManager(
        IAIProfileStore profileStore,
        IEnumerable<ICatalogEntryHandler<AIProfile>> handlers,
        ILogger<DefaultAIProfileManager> logger)
    : base(profileStore, handlers, logger)
    {
        _profileStore = profileStore;
    }

    /// <summary>
    /// Retrieves all AI profiles of the specified type, loading each profile through the handler pipeline.
    /// </summary>
    /// <param name="type">The profile type to filter by.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type, CancellationToken cancellationToken = default)
    {
        var profiles = await _profileStore.GetByTypeAsync(type);

        foreach (var profile in profiles)
        {
            await LoadAsync(profile, cancellationToken);
        }

        return profiles;
    }
}
