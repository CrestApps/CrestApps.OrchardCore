using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DncRegistry;

/// <summary>
/// Permissions for the DNC Registry module.
/// </summary>
public static class DncRegistryPermissions
{
    /// <summary>
    /// Gets the permission to manage DNC registry settings.
    /// </summary>
    public static readonly Permission ManageDncRegistrySettings = new(
        "ManageDncRegistrySettings",
        "Manage DNC registry settings");
}
