using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.Core.AI;

/// <summary>
/// Represents a successfully processed uploaded document together with its source file and generated chunks.
/// </summary>
public sealed class AIChatUploadedDocument
{
    /// <summary>
    /// Gets or sets the source file that was uploaded.
    /// </summary>
    public IFormFile File { get; set; }

    /// <summary>
    /// Gets or sets the persisted AI document entity.
    /// </summary>
    public AIDocument Document { get; set; }

    /// <summary>
    /// Gets or sets the chat-facing document metadata returned to the client.
    /// </summary>
    public ChatDocumentInfo DocumentInfo { get; set; }

    /// <summary>
    /// Gets or sets the generated chunks for the uploaded document.
    /// </summary>
    public IReadOnlyList<AIDocumentChunk> Chunks { get; set; } = [];
}
