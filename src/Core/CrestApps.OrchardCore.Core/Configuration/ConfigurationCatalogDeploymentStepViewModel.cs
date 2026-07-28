namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Represents the view model for a configuration catalog deployment step.
/// </summary>
public class ConfigurationCatalogDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether every catalog in the group is exported.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the selectable catalogs.
    /// </summary>
    public ConfigurationCatalogEntryViewModel[] Catalogs { get; set; }
}
