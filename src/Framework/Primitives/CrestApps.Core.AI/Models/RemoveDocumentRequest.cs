namespace CrestApps.Core.AI.Models;

/// <summary>
/// Describes a chat document removal request.
/// </summary>
public sealed class RemoveDocumentRequest
{
    /// <summary>
    /// Gets or sets the interaction or session identifier that owns the document.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the document to remove.
    /// </summary>
    public string DocumentId { get; set; }
}
