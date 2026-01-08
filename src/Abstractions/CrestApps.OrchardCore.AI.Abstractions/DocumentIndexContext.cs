using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Context for document indexing operations.
/// </summary>
public sealed class DocumentIndexContext
{
    /// <summary>
    /// Gets or sets the session/interaction identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public string DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the original file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the document chunks with embeddings.
    /// </summary>
    public IList<ChatInteractionDocumentChunk> Chunks { get; set; } = [];
}
