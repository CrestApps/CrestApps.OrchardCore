namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Describes the life cycle of a child tenant in the parent registry. The running or suspended state comes from the
/// tenant's shell settings.
/// </summary>
public enum ChildTenantStatus
{
    /// <summary>
    /// The tenant was created and its setup is running.
    /// </summary>
    Provisioning,

    /// <summary>
    /// The tenant is set up and can be used.
    /// </summary>
    Ready,

    /// <summary>
    /// The setup failed. The tenant can be set up again or discarded.
    /// </summary>
    Failed,

    /// <summary>
    /// The tenant is suspended and is kept until its retention date, then removed.
    /// </summary>
    PendingRemoval,

    /// <summary>
    /// The tenant is being removed.
    /// </summary>
    Removing,
}
