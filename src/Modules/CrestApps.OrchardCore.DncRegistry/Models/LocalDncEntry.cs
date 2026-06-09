namespace CrestApps.OrchardCore.DncRegistry.Models;

/// <summary>
/// Represents a batch of phone number entries in a local do-not-call list.
/// New documents store many phone numbers per document to reduce document-table usage
/// while keeping one index row per phone number for lookups.
/// </summary>
public sealed class LocalDncEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for this entry batch.
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
    /// Gets or sets the legacy single phone number stored by older documents.
    /// New batched documents leave this property empty and use <see cref="Records"/> instead.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the phone number records stored in this batch document.
    /// </summary>
    public List<LocalDncEntryRecord> Records { get; set; }
}
