namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents an attached document for chat interactions.
/// Used for "chat against own data" functionality.
/// </summary>
public sealed class ChatInteractionDocument
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
    /// Gets or sets the extracted text content from the document.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the size of the original file in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the document was uploaded.
    /// </summary>
    public DateTime UploadedUtc { get; set; }

    public List<DocumentChunk> ContentChunks { get; set; }
}
