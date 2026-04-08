namespace CrestApps.Core.AI.Models;

/// <summary>
/// Metadata stored on <see cref="AIProfile.Properties"/> to track
/// documents attached to the profile for RAG functionality.
/// </summary>
public sealed class DocumentsMetadata
{
    /// <summary>
    /// Gets or sets the collection of attached document metadata.
    /// </summary>
    public IList<ChatDocumentInfo> Documents { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of top matching document chunks to include in AI context.
    /// Default is 3 if not specified.
    /// </summary>
    public int? DocumentTopN { get; set; }
}
