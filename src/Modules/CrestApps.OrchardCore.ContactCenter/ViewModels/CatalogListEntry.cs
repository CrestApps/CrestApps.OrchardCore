namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents a single entry rendered by the shared <c>_CatalogList</c> partial.
/// </summary>
public class CatalogListEntry
{
    /// <summary>
    /// Gets or sets the display shape rendered for the entry.
    /// </summary>
    public object Shape { get; set; }

    /// <summary>
    /// Gets or sets the lowercase value used for client-side list filtering.
    /// </summary>
    public string FilterValue { get; set; }
}
