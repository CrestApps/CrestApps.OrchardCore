namespace CrestApps.Core.Infrastructure.Indexing.Models;

/// <summary>
/// Represents a document to be indexed in a search backend.
/// </summary>
public sealed class IndexDocument
{
    /// <summary>
    /// Gets or sets the unique identifier for this document.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the field values for this document.
    /// Keys are field names; values are the corresponding data.
    /// </summary>
    public IDictionary<string, object> Fields { get; set; } = new Dictionary<string, object>();
}
