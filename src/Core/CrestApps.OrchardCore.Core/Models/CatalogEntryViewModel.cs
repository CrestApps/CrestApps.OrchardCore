namespace CrestApps.OrchardCore.Core.Models;

/// <summary>
/// Represents the view model for catalog entry.
/// </summary>
public class CatalogEntryViewModel<T>
{
    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    public T Model { get; set; }

    /// <summary>
    /// Gets or sets the display shape associated with this catalog entry.
    /// Downstream consumers should cast to the concrete shape type.
    /// </summary>
    public object Shape { get; set; }
}
