using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Builds the results of the platform tools from an explicit list of fields. Shell settings hold the tenant's connection
/// string, table prefix and secret, so they are never serialized.
/// </summary>
internal static class TenantHierarchyPlatformToolResults
{
    /// <summary>
    /// Describes a tenant of the hierarchy.
    /// </summary>
    /// <param name="node">The tenant.</param>
    /// <returns>The description.</returns>
    public static object DescribeNode(HierarchyTreeNode node)
        => new
        {
            tenantName = node.Settings.Name,
            displayName = node.DisplayName,
            address = node.Address,
            state = node.Settings.State.ToString(),
            slug = node.Settings.GetHierarchySlug(),
            warnings = node.Warnings.Count > 0 ? node.Warnings : null,
        };

    /// <summary>
    /// Describes a tenant that can become a parent.
    /// </summary>
    /// <param name="settings">The tenant.</param>
    /// <returns>The description.</returns>
    public static object DescribeCandidate(ShellSettings settings)
        => new
        {
            tenantName = settings.Name,
            state = settings.State.ToString(),
            host = settings.GetPrimaryHost(),
            urlPrefix = string.IsNullOrEmpty(settings.RequestUrlPrefix) ? null : settings.RequestUrlPrefix,
        };

    /// <summary>
    /// Describes a parent policy, with the session times in the units of the policy editor.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <returns>The description.</returns>
    public static object DescribePolicy(ParentTenantPolicy policy)
        => new
        {
            maxChildren = policy.MaxChildren,
            childHostPattern = policy.ChildHostPattern,
            databaseStrategy = policy.DatabaseStrategy.ToString(),
            databasePool = policy.DatabasePool,
            recipes = policy.Recipes,
            blockedFeatures = policy.BlockedFeatures,
            deniedLocalPermissions = policy.DeniedLocalPermissions,
            requireMfa = policy.RequireMfa,
            sessionValidationMinutes = Math.Max(1, (int)policy.SessionValidationInterval.TotalMinutes),
            sessionIdleMinutes = Math.Max(1, (int)policy.SessionIdleTimeout.TotalMinutes),
            sessionLifetimeHours = Math.Max(1, (int)policy.SessionLifetime.TotalHours),
            switcherMode = policy.SwitcherMode.ToString(),
            parentLabel = policy.Labels?.Parent,
            childLabel = policy.Labels?.Child,
            childrenLabel = policy.Labels?.Children,
            removalGraceDays = policy.RemovalGraceDays,
        };
}
