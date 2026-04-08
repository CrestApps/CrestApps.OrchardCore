namespace CrestApps.Core;

/// <summary>
/// Marks a model as being associated with a named origin or provider,
/// enabling filtering and grouping by source (e.g., "OpenAI", "AzureOpenAI").
/// </summary>
public interface ISourceAwareModel
{
    /// <summary>
    /// Gets or sets the name of the source or provider that owns this model.
    /// </summary>
    string Source { get; set; }
}
