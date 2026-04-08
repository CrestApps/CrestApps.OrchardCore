namespace CrestApps.Core.AI.Models;

public sealed class ChatDocumentInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for this document.
    /// </summary>
    public string DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the original file name of the document.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the content type (MIME type) of the document.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the size of the original file in bytes.
    /// </summary>
    public long FileSize { get; set; }
}
