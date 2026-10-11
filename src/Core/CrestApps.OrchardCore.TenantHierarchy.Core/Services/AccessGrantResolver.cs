using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Works out the child roles a parent user gets in a child tenant from the grants that apply to the user.
/// </summary>
public static class AccessGrantResolver
{
    /// <summary>
    /// Returns the union of the child roles of every grant for the child tenant and of every grant for all child
    /// tenants. An empty result means the user may not enter the child tenant.
    /// </summary>
    /// <param name="grants">The grants that apply to the user, directly or through the user's roles.</param>
    /// <param name="childEntryId">The registry entry of the child tenant.</param>
    public static string[] ResolveChildRoles(IEnumerable<AccessGrant> grants, string childEntryId)
    {
        ArgumentException.ThrowIfNullOrEmpty(childEntryId);

        return (grants ?? [])
            .Where(grant => grant.ChildEntryId is null || string.Equals(grant.ChildEntryId, childEntryId, StringComparison.Ordinal))
            .SelectMany(grant => grant.ChildRoles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Returns the registry entries a user may enter, given every entry and the grants that apply to the user.
    /// </summary>
    /// <param name="grants">The grants that apply to the user.</param>
    /// <param name="entryIds">The registry entries of the parent.</param>
    public static IEnumerable<string> ResolveEnterableEntries(IEnumerable<AccessGrant> grants, IEnumerable<string> entryIds)
    {
        var grantList = (grants ?? [])
            .Where(grant => grant.ChildRoles?.Any(role => !string.IsNullOrWhiteSpace(role)) == true)
            .ToList();

        if (grantList.Any(grant => grant.ChildEntryId is null))
        {
            return entryIds ?? [];
        }

        var granted = grantList
            .Select(grant => grant.ChildEntryId)
            .ToHashSet(StringComparer.Ordinal);

        return (entryIds ?? []).Where(granted.Contains);
    }
}
