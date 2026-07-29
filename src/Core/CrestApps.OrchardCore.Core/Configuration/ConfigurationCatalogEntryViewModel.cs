namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Represents a selectable configuration catalog in a deployment step editor.
/// </summary>
public class ConfigurationCatalogEntryViewModel
{
    /// <summary>
    /// Gets or sets the recipe step name of the catalog.
    /// </summary>
    public string StepName { get; set; }

    /// <summary>
    /// Gets or sets the text shown to the operator.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the catalog is exported.
    /// </summary>
    public bool IsSelected { get; set; }
}
