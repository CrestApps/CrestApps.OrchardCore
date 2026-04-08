using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.Core.AI;

/// <summary>
/// Provides context for a chat document upload operation.
/// </summary>
public sealed class AIChatDocumentUploadContext
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
    /// Gets or sets the AI profile associated with the upload.
    /// </summary>
    public AIProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets the reference identifier used for document storage.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the reference type used for document storage.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets whether the upload created a new chat session.
    /// </summary>
    public bool IsNewSession { get; set; }

    /// <summary>
    /// Gets or sets the successfully uploaded documents.
    /// </summary>
    public IReadOnlyList<AIChatUploadedDocument> UploadedDocuments { get; set; } = [];
}
