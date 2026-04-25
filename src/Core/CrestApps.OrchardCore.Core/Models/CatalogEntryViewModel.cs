namespace CrestApps.OrchardCore.Core.Models;

public class CatalogEntryViewModel<T>
{
    public T Model { get; set; }

    /// <summary>
    /// Gets or sets the display shape associated with this catalog entry.
    /// Downstream consumers should cast to the concrete shape type.
    /// </summary>
    public object Shape { get; set; }
}
