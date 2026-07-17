namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a single server-side approved external transfer destination in the tenant catalog.
/// </summary>
public sealed class ContactCenterExternalDestination
{
    /// <summary>
    /// Gets or sets the opaque stable identifier for this destination entry.
    /// Callers supply this identifier when requesting an external transfer; the resolver
    /// looks up the entry by this value to obtain the canonical E.164 address.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the operator-assigned display name for this destination.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the server-side canonical E.164 address used for the actual transfer.
    /// This value is never supplied by callers; it is always taken from this stored entry.
    /// </summary>
    public string E164Address { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this destination is active.
    /// Disabled entries are always denied even when a caller supplies the matching identifier.
    /// </summary>
    public bool Enabled { get; set; }
}
