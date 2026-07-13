namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents one provider directory destination available for call transfer.
/// </summary>
public sealed class TelephonyDirectoryEntry
{
    /// <summary>
    /// Gets or sets the provider-specific entry identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the human-readable entry name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the destination sent to the provider when transferring a call.
    /// </summary>
    public string Destination { get; set; }

    /// <summary>
    /// Gets or sets the entry's internal extension, when available.
    /// </summary>
    public string Extension { get; set; }

    /// <summary>
    /// Gets or sets the entry's external phone number, when available.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets provider-specific status or grouping text.
    /// </summary>
    public string Detail { get; set; }
}
