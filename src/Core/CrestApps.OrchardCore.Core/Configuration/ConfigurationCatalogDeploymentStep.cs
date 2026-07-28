using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Represents a deployment step that exports the configuration catalogs of a single group.
/// </summary>
public abstract class ConfigurationCatalogDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Gets or sets a value indicating whether every catalog in the group is exported.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the step names of the catalogs to export when <see cref="IncludeAll"/> is disabled.
    /// </summary>
    public string[] CatalogNames { get; set; }
}
