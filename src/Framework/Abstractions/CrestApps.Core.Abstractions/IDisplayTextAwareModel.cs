namespace CrestApps.Core;

/// <summary>
/// Marks a model as having a human-readable display text property,
/// enabling consistent rendering in lists, dropdowns, and search results.
/// </summary>
public interface IDisplayTextAwareModel
{
    /// <summary>
    /// Gets or sets the human-readable display text for this model.
    /// </summary>
    string DisplayText { get; set; }
}
