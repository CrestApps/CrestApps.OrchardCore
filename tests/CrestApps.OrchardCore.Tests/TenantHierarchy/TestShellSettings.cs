using CrestApps.OrchardCore.TenantHierarchy;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

/// <summary>
/// Builds shell settings for the tenant hierarchy unit tests.
/// </summary>
internal static class TestShellSettings
{
    /// <summary>
    /// Creates the settings of the Default tenant.
    /// </summary>
    public static ShellSettings Default()
        => Create(ShellSettings.DefaultShellName, "default-id");

    /// <summary>
    /// Creates the settings of an ordinary tenant.
    /// </summary>
    /// <param name="name">The tenant name.</param>
    public static ShellSettings Ordinary(string name)
        => Create(name, $"{name}-id");

    /// <summary>
    /// Creates the settings of a parent tenant.
    /// </summary>
    /// <param name="name">The tenant name.</param>
    public static ShellSettings Parent(string name)
    {
        var settings = Create(name, $"{name}-id");
        settings[TenantHierarchyConstants.SettingsKeys.Role] = TenantHierarchyConstants.Roles.Parent;
        settings[TenantHierarchyConstants.SettingsKeys.Slug] = name;
        settings.RequestUrlHost = $"{name}.platform.com";

        return settings;
    }

    /// <summary>
    /// Creates the settings of a child tenant of a parent.
    /// </summary>
    /// <param name="name">The tenant name.</param>
    /// <param name="parent">The parent settings.</param>
    public static ShellSettings Child(string name, ShellSettings parent)
    {
        var settings = Create(name, $"{name}-id");
        settings[TenantHierarchyConstants.SettingsKeys.Role] = TenantHierarchyConstants.Roles.Child;
        settings[TenantHierarchyConstants.SettingsKeys.ParentTenantId] = parent.TenantId;
        settings[TenantHierarchyConstants.SettingsKeys.ParentTenant] = parent.Name;
        settings.RequestUrlHost = $"{name}.{parent.RequestUrlHost}";

        return settings;
    }

    private static ShellSettings Create(string name, string tenantId)
    {
        var settings = new ShellSettings
        {
            Name = name,
            State = TenantState.Running,
        };

        // The tenant identifier comes from the first version identifier.
        settings.VersionId = tenantId;

        return settings;
    }
}
