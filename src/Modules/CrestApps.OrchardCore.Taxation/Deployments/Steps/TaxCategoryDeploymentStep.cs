using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax categories.
/// </summary>
public sealed class TaxCategoryDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TaxCategoryDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TaxCategoryDeploymentStep>("Tax Categories");

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxCategoryDeploymentStep"/> class.
    /// </summary>
    public TaxCategoryDeploymentStep()
    {
        Name = TaxationDeploymentSteps.TaxCategory;
        Category = _category;
        Title = _title;
    }
}
