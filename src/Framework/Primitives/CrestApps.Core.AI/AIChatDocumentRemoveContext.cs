using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.Core.AI;

/// <summary>
/// Provides context for a chat document removal operation.
/// </summary>
public sealed class AIChatDocumentRemoveContext
{
    /// <summary>
    /// Gets or sets the current HTTP context.
    /// </summary>
    public HttpContext HttpContext { get; set; }

    /// <summary>
    /// Gets or sets the chat interaction being modified.
    /// </summary>
    public ChatInteraction Interaction { get; set; }

    /// <summary>
    /// Gets or sets the chat session being modified.
    /// </summary>
    public AIChatSession Session { get; set; }

    /// <summary>
    /// Gets or sets the AI profile associated with the removal.
    /// </summary>
    public AIProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets the removed document metadata from the chat model.
    /// </summary>
    public ChatDocumentInfo DocumentInfo { get; set; }

    /// <summary>
    /// Gets or sets the removed AI document entity.
    /// </summary>
    public AIDocument Document { get; set; }

    /// <summary>
    /// Gets or sets the chunk identifiers that were removed for the document.
    /// </summary>
    public IReadOnlyList<string> ChunkIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the reference identifier used for document storage.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the reference type used for document storage.
    /// </summary>
    public string ReferenceType { get; set; }
}
