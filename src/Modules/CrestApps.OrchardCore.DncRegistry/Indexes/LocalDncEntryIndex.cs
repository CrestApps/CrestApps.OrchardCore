using YesSql.Indexes;

namespace CrestApps.OrchardCore.DncRegistry.Indexes;

/// <summary>
/// YesSql map index for querying local DNC phone number entries.
/// </summary>
public sealed class LocalDncEntryIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the unique entry identifier.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the list this entry belongs to.
    /// </summary>
    public string ListId { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the normalized phone number (digits only).
    /// </summary>
    public string PhoneNumber { get; set; }
}
