using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// The search, state filter and sort of the child tenants list, shared by the list page and the AI tools.
/// </summary>
internal static class ChildTenantListFilter
{
    /// <summary>
    /// Filters and sorts child tenants: the search matches the name, host or description, the state is the list's state
    /// filter and the order is the list's sort.
    /// </summary>
    /// <param name="items">The child tenants.</param>
    /// <param name="options">The search, state filter and sort.</param>
    /// <returns>The matching child tenants, in order.</returns>
    public static IEnumerable<ChildTenantInfo> Apply(IEnumerable<ChildTenantInfo> items, ChildTenantListOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            var search = options.Search.Trim();
            items = items.Where(item =>
                (item.Entry.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (item.Entry.Host?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (item.Entry.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        items = options.State switch
        {
            ChildTenantStateFilter.Running => items.Where(item => item.State == ChildTenantRuntimeState.Running && item.Entry.Status == ChildTenantStatus.Ready),
            ChildTenantStateFilter.Suspended => items.Where(item => item.State == ChildTenantRuntimeState.Suspended && item.Entry.Status == ChildTenantStatus.Ready),
            ChildTenantStateFilter.SettingUp => items.Where(item => item.Entry.Status == ChildTenantStatus.Provisioning || item.State == ChildTenantRuntimeState.Initializing),
            ChildTenantStateFilter.Failed => items.Where(item => item.Entry.Status == ChildTenantStatus.Failed),
            ChildTenantStateFilter.PendingRemoval => items.Where(item => item.Entry.Status is ChildTenantStatus.PendingRemoval or ChildTenantStatus.Removing),
            ChildTenantStateFilter.ChangedByPlatform => items.Where(item => item.State == ChildTenantRuntimeState.ChangedByPlatform),
            _ => items,
        };

        return options.OrderBy switch
        {
            ChildTenantSort.Newest => items.OrderByDescending(item => item.Entry.CreatedUtc),
            ChildTenantSort.State => items.OrderBy(item => item.State).ThenBy(item => item.Entry.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            _ => items.OrderBy(item => item.Entry.DisplayName, StringComparer.CurrentCultureIgnoreCase),
        };
    }
}
