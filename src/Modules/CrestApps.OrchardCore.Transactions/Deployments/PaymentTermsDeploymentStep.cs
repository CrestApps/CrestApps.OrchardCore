using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Transactions.Deployments;

/// <summary>
/// Exports every payment term.
/// </summary>
public sealed class PaymentTermsDeploymentStep : DeploymentStep
{
    /// <summary>
    /// The name of the step, the same in the deployment plan and in the recipe.
    /// </summary>
    public const string StepName = "PaymentTerms";

    private static readonly LocalizationSource _category = LocalizationSource.Create<PaymentTermsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<PaymentTermsDeploymentStep>("Payment terms");

    /// <inheritdoc/>
    public override string Name => StepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
