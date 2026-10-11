using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax types.
/// </summary>
public sealed class TaxTypeDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TaxTypeDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TaxTypeDeploymentStep>("Tax Types");

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxTypeDeploymentStep"/> class.
    /// </summary>
    public TaxTypeDeploymentStep()
    {
        Name = TaxationDeploymentSteps.TaxType;
        Category = _category;
        Title = _title;
    }
}
