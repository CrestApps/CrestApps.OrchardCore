using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.Extensions.Configuration;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy;

/// <summary>
/// Reads the tenant hierarchy facts that are stored in the shell settings of a tenant.
/// </summary>
public static class TenantHierarchyShellSettingsExtensions
{
    /// <summary>
    /// Returns the hierarchy role of the tenant, or <see langword="null"/> when the tenant is not part of a hierarchy.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    public static string GetHierarchyRole(this ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var role = settings[TenantHierarchyConstants.SettingsKeys.Role];

        if (string.Equals(role, TenantHierarchyConstants.Roles.Parent, StringComparison.OrdinalIgnoreCase))
        {
            return TenantHierarchyConstants.Roles.Parent;
        }

        if (string.Equals(role, TenantHierarchyConstants.Roles.Child, StringComparison.OrdinalIgnoreCase))
        {
            return TenantHierarchyConstants.Roles.Child;
        }

        return null;
    }

    /// <summary>
    /// Returns whether the tenant is a parent tenant.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    public static bool IsParentTenant(this ShellSettings settings)
        => settings.GetHierarchyRole() == TenantHierarchyConstants.Roles.Parent;

    /// <summary>
    /// Returns whether the tenant is a child tenant.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    public static bool IsChildTenant(this ShellSettings settings)
        => settings.GetHierarchyRole() == TenantHierarchyConstants.Roles.Child;

    /// <summary>
    /// Returns whether the tenant is a parent or a child tenant.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    public static bool IsInTenantHierarchy(this ShellSettings settings)
        => settings.GetHierarchyRole() != null;

    /// <summary>
    /// Returns the tenant identifier of the parent of a child tenant.
    /// </summary>
    /// <param name="settings">The shell settings of the child tenant.</param>
    public static string GetParentTenantId(this ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings[TenantHierarchyConstants.SettingsKeys.ParentTenantId];
    }

    /// <summary>
    /// Returns whether the tenant is a child of the given parent tenant.
    /// </summary>
    /// <param name="settings">The shell settings of the child tenant.</param>
    /// <param name="parent">The shell settings of the parent tenant.</param>
    public static bool IsChildOf(this ShellSettings settings, ShellSettings parent)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(parent);

        return settings.IsChildTenant() &&
            parent.IsParentTenant() &&
            !string.IsNullOrEmpty(parent.TenantId) &&
            string.Equals(settings.GetParentTenantId(), parent.TenantId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the hierarchy slug of the tenant.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    public static string GetHierarchySlug(this ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings[TenantHierarchyConstants.SettingsKeys.Slug];
    }

    /// <summary>
    /// Returns the identifier of the parent's registry entry of a child tenant.
    /// </summary>
    /// <param name="settings">The shell settings of the child tenant.</param>
    public static string GetHierarchyEntryId(this ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings[TenantHierarchyConstants.SettingsKeys.EntryId];
    }

    /// <summary>
    /// Returns the display name of a parent tenant, or its name when no display name is set.
    /// </summary>
    /// <param name="settings">The shell settings of the parent tenant.</param>
    public static string GetHierarchyDisplayName(this ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var displayName = settings[TenantHierarchyConstants.SettingsKeys.DisplayName];

        return string.IsNullOrWhiteSpace(displayName)
            ? settings.Name
            : displayName;
    }

    /// <summary>
    /// Returns the primary host of the tenant, or <see langword="null"/> when it has no host.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    public static string GetPrimaryHost(this ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var hosts = settings.RequestUrlHosts;

        return hosts.Length > 0
            ? hosts[0]
            : null;
    }

    /// <summary>
    /// Reads the policy of a parent tenant. Missing values keep their defaults.
    /// </summary>
    /// <param name="settings">The shell settings of the parent tenant.</param>
    public static ParentTenantPolicy GetParentPolicy(this ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var policy = new ParentTenantPolicy();
        settings.ShellConfiguration.GetSection(TenantHierarchyConstants.SettingsKeys.Policy).Bind(policy);
        policy.Labels ??= new TenantHierarchyLabels();
        policy.Recipes = Clean(policy.Recipes);
        policy.BlockedFeatures = Clean(policy.BlockedFeatures);
        policy.DeniedLocalPermissions = Clean(policy.DeniedLocalPermissions);

        if (policy.MaxChildren < 0)
        {
            policy.MaxChildren = 0;
        }

        return policy;
    }

    private static string[] Clean(string[] values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    /// <summary>
    /// Reads the permissions denied to the local users of a child tenant.
    /// </summary>
    /// <param name="settings">The shell settings of the child tenant.</param>
    public static string[] GetDeniedLocalPermissions(this ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.ShellConfiguration
            .GetSection(TenantHierarchyConstants.SettingsKeys.DeniedLocalPermissions)
            .GetChildren()
            .Select(section => section.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
}
