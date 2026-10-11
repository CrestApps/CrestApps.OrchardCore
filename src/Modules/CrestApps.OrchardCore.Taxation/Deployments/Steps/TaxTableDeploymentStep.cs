using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax tables.
/// </summary>
public sealed class TaxTableDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TaxTableDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TaxTableDeploymentStep>("Tax Tables");

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxTableDeploymentStep"/> class.
    /// </summary>
    public TaxTableDeploymentStep()
    {
        Name = TaxationDeploymentSteps.TaxTable;
        Category = _category;
        Title = _title;
    }
}
