namespace CrestApps.OrchardCore.Core.Models;

public class EditCatalogEntryViewModel
{
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the editor shape used to render the catalog entry form.
    /// Downstream consumers should cast to the concrete editor type.
    /// </summary>
    public object Editor { get; set; }
}
