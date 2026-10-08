namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// View model for a single approved external transfer destination catalog entry.
/// </summary>
public class ContactCenterExternalDestinationViewModel
{
    /// <summary>
    /// Gets or sets the opaque stable identifier for this destination.
    /// A new entry with an empty identifier receives an auto-generated identifier on save.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the operator-assigned display name shown in the admin UI.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the canonical E.164 phone number for this destination,
    /// for example <c>+15551234567</c>.
    /// </summary>
    public string E164Address { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this destination is currently active.
    /// </summary>
    public bool Enabled { get; set; }
}
