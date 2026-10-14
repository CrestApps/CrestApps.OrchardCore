using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The model of the tenant picker: the child tenants the user may enter, with favorites and recent ones first.
/// </summary>
public class TenantSwitchViewModel
{
    /// <summary>
    /// Gets or sets the favorite child tenants.
    /// </summary>
    public List<TenantSwitchItem> Favorites { get; set; } = [];

    /// <summary>
    /// Gets or sets the recent child tenants that are not favorites.
    /// </summary>
    public List<TenantSwitchItem> Recent { get; set; } = [];

    /// <summary>
    /// Gets or sets every child tenant the user may enter, by name.
    /// </summary>
    public List<TenantSwitchItem> All { get; set; } = [];

    /// <summary>
    /// Gets or sets the labels of the parent.
    /// </summary>
    public HierarchyLabels Labels { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the picker is framed by a child tenant.
    /// </summary>
    public bool Embedded { get; set; }

    /// <summary>
    /// Gets or sets the registry entry of the child tenant the user is in, when the picker is framed.
    /// </summary>
    public string CurrentEntryId { get; set; }

    /// <summary>
    /// Gets or sets the number of sessions that were just ended by "Sign out everywhere".
    /// </summary>
    public int? SignedOutCount { get; set; }

    /// <summary>
    /// Builds the model from the child tenants a user may enter and the user's preferences.
    /// </summary>
    /// <param name="enterable">The child tenants the user may enter.</param>
    /// <param name="preference">The user's favorites and recent child tenants.</param>
    /// <param name="labels">The labels of the parent.</param>
    /// <param name="embedded">Whether the picker is framed by a child tenant.</param>
    public static TenantSwitchViewModel Create(
        IReadOnlyList<ChildTenantInfo> enterable,
        TenantSwitcherPreference preference,
        HierarchyLabels labels,
        bool embedded)
    {
        ArgumentNullException.ThrowIfNull(enterable);

        var favorites = (preference?.Favorites ?? []).ToHashSet(StringComparer.Ordinal);
        var recent = preference?.Recent ?? [];

        var items = enterable
            .Select(info => new TenantSwitchItem
            {
                Info = info,
                IsFavorite = favorites.Contains(info.Entry.EntryId),
                RecentRank = recent.IndexOf(info.Entry.EntryId) is var rank and >= 0 ? rank : null,
            })
            .ToList();

        return new TenantSwitchViewModel
        {
            Favorites = items
                .Where(item => item.IsFavorite)
                .OrderBy(item => item.Info.Entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            Recent = items
                .Where(item => !item.IsFavorite && item.RecentRank.HasValue)
                .OrderBy(item => item.RecentRank)
                .Take(5)
                .ToList(),
            All = items
                .OrderBy(item => item.Info.Entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            Labels = labels,
            Embedded = embedded,
        };
    }
}
