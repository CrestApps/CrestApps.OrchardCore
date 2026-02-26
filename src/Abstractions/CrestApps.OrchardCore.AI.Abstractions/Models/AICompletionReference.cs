namespace CrestApps.OrchardCore.AI.Models;

public sealed class AICompletionReference
{
    public string Text { get; set; }

    public string Link { get; set; }

    public string Title { get; set; }

    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the raw reference identifier from the source index.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the type of the reference source
    /// (e.g., the source index profile type for data sources, or "Document" for uploaded documents).
    /// Used to determine how links should be generated.
    /// </summary>
    public string ReferenceType { get; set; }
}
