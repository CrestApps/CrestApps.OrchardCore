using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax rules.
/// </summary>
public sealed class TaxRuleDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxRuleDeploymentStep"/> class.
    /// </summary>
    public TaxRuleDeploymentStep()
    {
        Name = TaxationDeploymentSteps.TaxRule;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxRuleDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxRuleDeploymentStep(IStringLocalizer<TaxRuleDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Taxation"];
    }
}
