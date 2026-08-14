namespace CrestApps.OrchardCore.DncRegistry.Models;

/// <summary>
/// Represents a single phone number entry in a local do-not-call list.
/// Phone numbers are stored in a normalized (digits-only) format for consistent matching.
/// </summary>
public sealed class LocalDncEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for this entry.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the list this entry belongs to.
    /// </summary>
    public string ListId { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code this entry is associated with.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the normalized phone number (digits only).
    /// </summary>
    public string PhoneNumber { get; set; }
}
