namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Describes the live state of a child tenant, read from its shell settings.
/// </summary>
public enum ChildTenantRuntimeState
{
    /// <summary>
    /// The tenant is running.
    /// </summary>
    Running,

    /// <summary>
    /// The tenant is disabled. In the parent it is shown as suspended.
    /// </summary>
    Suspended,

    /// <summary>
    /// The tenant exists but is not set up.
    /// </summary>
    Uninitialized,

    /// <summary>
    /// The tenant is being set up.
    /// </summary>
    Initializing,

    /// <summary>
    /// The registry entry no longer matches a tenant of this parent. The platform removed or moved the tenant.
    /// </summary>
    ChangedByPlatform,
}
