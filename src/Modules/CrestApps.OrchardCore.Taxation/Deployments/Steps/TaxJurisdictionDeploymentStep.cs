using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax jurisdictions.
/// </summary>
public sealed class TaxJurisdictionDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TaxJurisdictionDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TaxJurisdictionDeploymentStep>("Tax Jurisdictions");

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxJurisdictionDeploymentStep"/> class.
    /// </summary>
    public TaxJurisdictionDeploymentStep()
    {
        Name = TaxationDeploymentSteps.TaxJurisdiction;
        Category = _category;
        Title = _title;
    }
}
