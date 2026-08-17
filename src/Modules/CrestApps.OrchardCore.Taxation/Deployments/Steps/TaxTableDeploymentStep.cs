using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax tables.
/// </summary>
public sealed class TaxTableDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxTableDeploymentStep"/> class.
    /// </summary>
    public TaxTableDeploymentStep()
    {
        Name = TaxationDeploymentSteps.TaxTable;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxTableDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxTableDeploymentStep(IStringLocalizer<TaxTableDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Taxation"];
    }
}
