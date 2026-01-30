using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents an attached document for chat interactions.
/// Used for "chat against own data" functionality.
/// </summary>
public sealed class ChatInteractionDocument : CatalogItem
{
    /// <summary>
    /// Gets or sets the ChatInteractionId the document belongs to.
    /// </summary>
    public string ChatInteractionId { get; set; }

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
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the size of the original file in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the document was uploaded.
    /// </summary>
    public DateTime UploadedUtc { get; set; }

    /// <summary>
    /// Gets or sets the extracted text as chunks from the document.
    /// </summary>
    public List<ChatInteractionDocumentChunk> Chunks { get; set; }
}
