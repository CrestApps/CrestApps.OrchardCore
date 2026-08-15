using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax jurisdictions.
/// </summary>
public sealed class TaxJurisdictionDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxJurisdictionDeploymentStep"/> class.
    /// </summary>
    public TaxJurisdictionDeploymentStep()
    {
        Name = TaxationDeploymentSteps.TaxJurisdiction;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxJurisdictionDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxJurisdictionDeploymentStep(IStringLocalizer<TaxJurisdictionDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Taxation"];
    }
}
