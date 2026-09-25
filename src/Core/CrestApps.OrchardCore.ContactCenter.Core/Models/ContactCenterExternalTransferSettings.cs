namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Tenant-scoped site settings that hold the operator-curated catalog of approved external
/// transfer destinations.  Stored via Orchard Core site settings so the catalog is isolated
/// per shell/tenant and never shared across tenants.
/// </summary>
public sealed class ContactCenterExternalTransferSettings
{
    /// <summary>
    /// Gets or sets the list of approved external destinations configured for this tenant.
    /// Only entries that are present and enabled are reachable via an external transfer.
    /// </summary>
    public List<ContactCenterExternalDestination> Destinations { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether an agent who may transfer externally may also send a caller to a
    /// number that is not in <see cref="Destinations"/>, typed into the soft phone. Off by default: only the curated
    /// catalog is reachable until an administrator opts in. Even when on, the number must pass the platform's dial
    /// policy (no emergency or premium-rate numbers) and may not be one of the contact center's own numbers.
    /// </summary>
    public bool AllowUnlistedNumbers { get; set; }
}
