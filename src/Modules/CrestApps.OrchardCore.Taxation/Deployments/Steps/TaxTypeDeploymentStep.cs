using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax types.
/// </summary>
public sealed class TaxTypeDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxTypeDeploymentStep"/> class.
    /// </summary>
    public TaxTypeDeploymentStep()
    {
        Name = TaxationDeploymentSteps.TaxType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxTypeDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxTypeDeploymentStep(IStringLocalizer<TaxTypeDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Taxation"];
    }
}
