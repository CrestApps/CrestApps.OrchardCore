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

    /// <inheritdoc />
    public override string Name => TaxationDeploymentSteps.TaxRule;

    /// <inheritdoc />
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    public override LocalizationSource Title => _title;
}
