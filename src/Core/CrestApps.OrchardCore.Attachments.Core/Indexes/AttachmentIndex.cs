using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Attachments.Core.Indexes;

/// <summary>
/// Indexes attachments by the record they are attached to.
/// </summary>
public sealed class AttachmentIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the record's kind.
    /// </summary>
    public string OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the record's id.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the media type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the size, in bytes.
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// Gets or sets when it was uploaded, in UTC.
    /// </summary>
    public DateTime UploadedUtc { get; set; }
}
