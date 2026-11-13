using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.SiteResources.Deployments;

/// <summary>
/// Exports the site's stylesheets and scripts.
/// </summary>
public sealed class SiteResourcesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<SiteResourcesDeploymentStep>("Design");
    private static readonly LocalizationSource _title = LocalizationSource.Create<SiteResourcesDeploymentStep>("Site resources");

    /// <inheritdoc />
    public override string Name => SiteResourcesConstants.RecipeStepName;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
