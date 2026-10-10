namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Joins a registry entry with the live shell settings of its child tenant.
/// </summary>
public sealed class ChildTenantInfo
{
    /// <summary>
    /// Gets or sets the registry entry.
    /// </summary>
    public ChildTenantEntry Entry { get; set; }

    /// <summary>
    /// Gets or sets the live state of the child tenant.
    /// </summary>
    public ChildTenantRuntimeState State { get; set; }

    /// <summary>
    /// Gets or sets the base address of the child tenant, for example <c>https://business1.firma.platform.com</c>.
    /// It is <see langword="null"/> when the state is <see cref="ChildTenantRuntimeState.ChangedByPlatform"/>.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets a value indicating whether the child tenant can be entered.
    /// </summary>
    public bool CanEnter => State == ChildTenantRuntimeState.Running && Entry?.Status == ChildTenantStatus.Ready;
}
