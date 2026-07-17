using System.Collections.Generic;

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
}
