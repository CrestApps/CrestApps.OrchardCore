using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// Indexes the published and latest versions of lead content items by their lead state.
/// </summary>
public sealed class LeadIndex : MapIndex
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
    /// Gets or sets the lead status identifier.
    /// </summary>
    public string StatusId { get; set; }

    /// <summary>
    /// Gets or sets whether the lead's status is closed.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Gets or sets whether the lead was converted.
    /// </summary>
    public bool IsConverted { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the lead source.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the list the lead arrived in.
    /// </summary>
    public string ListName { get; set; }

    /// <summary>
    /// Gets or sets the lead rating.
    /// </summary>
    public string Rating { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who owns the lead.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the contact the lead was converted into.
    /// </summary>
    public string ConvertedContactItemId { get; set; }

    /// <summary>
    /// Gets or sets when the lead was converted.
    /// </summary>
    public DateTime? ConvertedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the lead was created.
    /// </summary>
    public DateTime? CreatedUtc { get; set; }
}
