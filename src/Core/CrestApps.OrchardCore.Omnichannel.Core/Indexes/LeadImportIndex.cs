using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// Indexes the published and latest versions of lead content items by the file imports they arrived in. A lead
/// imported by more than one file has one row per file.
/// </summary>
public sealed class LeadImportIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the content item id.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the lead content type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets whether the indexed version is published.
    /// </summary>
    public bool Published { get; set; }

    /// <summary>
    /// Gets or sets whether the indexed version is the latest version.
    /// </summary>
    public bool Latest { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the import entry.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets the name of the imported file.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets when the file was uploaded, in UTC.
    /// </summary>
    public DateTime ImportedUtc { get; set; }
}
