using CrestApps.Core.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Attachments.Models;

/// <summary>
/// A file attached to a record, such as a photo on a ticket or a document on a work order. The bytes are kept
/// encrypted in the tenant's file store; this record says what the file is and whose it is.
/// </summary>
public sealed class Attachment : CatalogItem
{
    /// <summary>
    /// Gets or sets the record the file is attached to.
    /// </summary>
    public RecordRef Owner { get; set; }

    /// <summary>
    /// Gets or sets the file's name, without a folder.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the media type, as the file's own bytes show it.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the size, in bytes.
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// Gets or sets whether the file is a picture that may be shown inline.
    /// </summary>
    public bool IsImage { get; set; }

    /// <summary>
    /// Gets or sets who uploaded it; empty for the site itself, such as a file that came in by e-mail.
    /// </summary>
    public PartyRef UploadedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was uploaded, in UTC.
    /// </summary>
    public DateTime UploadedUtc { get; set; }
}
