using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax categories.
/// </summary>
public sealed class TaxCategoryDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxCategoryDeploymentStep"/> class.
    /// </summary>
    public TaxCategoryDeploymentStep()
    {
        Name = TaxationDeploymentSteps.TaxCategory;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxCategoryDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxCategoryDeploymentStep(IStringLocalizer<TaxCategoryDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Taxation"];
    }
}
