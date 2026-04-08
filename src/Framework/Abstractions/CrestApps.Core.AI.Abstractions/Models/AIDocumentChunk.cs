using CrestApps.Core.Models;

namespace CrestApps.Core.AI.Models;

/// <summary>
/// Represents a single chunk of text extracted from an <see cref="AIDocument"/>.
/// Stored as a separate record to avoid bloating the parent <see cref="AIDocument"/>
/// with large embedding arrays.
/// </summary>
public sealed class AIDocumentChunk : CatalogItem
{
    /// <summary>
    /// Gets or sets the identifier of the parent <see cref="AIDocument"/>.
    /// </summary>
    public string AIDocumentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the owning resource (e.g., AI Profile ID or Chat Interaction ID).
    /// Denormalized from the parent document for efficient query access.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the type of the owning resource (e.g., "profile" or "chatinteraction").
    /// Denormalized from the parent document for efficient query access.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the text content of this chunk.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the embedding vector for this chunk.
    /// Stored alongside the content to avoid regenerating embeddings
    /// when the vector index is rebuilt or re-indexed.
    /// </summary>
    public float[] Embedding { get; set; }

    /// <summary>
    /// Gets or sets the chunk index within the parent document.
    /// </summary>
    public int Index { get; set; }
}
