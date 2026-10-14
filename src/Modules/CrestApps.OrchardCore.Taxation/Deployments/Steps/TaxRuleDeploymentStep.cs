using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports tax rules.
/// </summary>
public sealed class TaxRuleDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TaxRuleDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TaxRuleDeploymentStep>("Tax Rules");

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxRuleDeploymentStep"/> class.
    /// </summary>
    public TaxRuleDeploymentStep()
    {
        Name = TaxationDeploymentSteps.TaxRule;
        Category = _category;
        Title = _title;
    }
}
