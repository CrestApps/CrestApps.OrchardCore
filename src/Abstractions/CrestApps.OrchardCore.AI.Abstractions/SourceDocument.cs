namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Represents a document read from a source index with extracted title, content, and all source fields.
/// </summary>
public sealed class SourceDocument
{
    /// <summary>
    /// Gets or sets the document title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the document content text.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets all fields from the source document.
    /// Used for populating filter fields in the knowledge base index.
    /// </summary>
    public Dictionary<string, object> Fields { get; set; }
}
