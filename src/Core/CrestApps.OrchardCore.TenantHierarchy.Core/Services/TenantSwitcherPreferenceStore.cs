using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Stores the favorite and recent child tenants of parent users.
/// </summary>
public sealed class TenantSwitcherPreferenceStore
{
    private const string Collection = TenantHierarchyConstants.CollectionName;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantSwitcherPreferenceStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session of the parent tenant.</param>
    public TenantSwitcherPreferenceStore(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Returns the preference of a user, or a new one.
    /// </summary>
    /// <param name="userId">The parent user.</param>
    public async Task<TenantSwitcherPreference> GetAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var preference = await _session.Query<TenantSwitcherPreference, TenantSwitcherPreferenceIndex>(index => index.UserId == userId, Collection)
            .FirstOrDefaultAsync();

        return preference ?? new TenantSwitcherPreference
        {
            UserId = userId,
        };
    }

    /// <summary>
    /// Moves a child tenant to the top of the recent list of a user.
    /// </summary>
    /// <param name="userId">The parent user.</param>
    /// <param name="entryId">The registry entry of the child tenant.</param>
    public async Task AddRecentAsync(string userId, string entryId)
    {
        var preference = await GetAsync(userId);
        preference.Recent.Remove(entryId);
        preference.Recent.Insert(0, entryId);

        while (preference.Recent.Count > TenantSwitcherPreference.MaxRecent)
        {
            preference.Recent.RemoveAt(preference.Recent.Count - 1);
        }

        await _session.SaveAsync(preference, checkConcurrency: false, Collection);
    }

    /// <summary>
    /// Adds a child tenant to the favorites of a user, or removes it. Returns whether it is now a favorite.
    /// </summary>
    /// <param name="userId">The parent user.</param>
    /// <param name="entryId">The registry entry of the child tenant.</param>
    public async Task<bool> ToggleFavoriteAsync(string userId, string entryId)
    {
        var preference = await GetAsync(userId);
        var isFavorite = !preference.Favorites.Remove(entryId);

        if (isFavorite)
        {
            preference.Favorites.Add(entryId);
        }

        await _session.SaveAsync(preference, checkConcurrency: false, Collection);

        return isFavorite;
    }
}
