namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The state filters of the child tenants list.
/// </summary>
public enum ChildTenantStateFilter
{
    /// <summary>
    /// Every child tenant.
    /// </summary>
    All,

    /// <summary>
    /// Running child tenants.
    /// </summary>
    Running,

    /// <summary>
    /// Suspended child tenants.
    /// </summary>
    Suspended,

    /// <summary>
    /// Child tenants that are being set up.
    /// </summary>
    SettingUp,

    /// <summary>
    /// Child tenants whose setup failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Child tenants waiting to be removed.
    /// </summary>
    PendingRemoval,

    /// <summary>
    /// Child tenants the platform removed or moved.
    /// </summary>
    ChangedByPlatform,
}
