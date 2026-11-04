using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Markets.Deployments;

/// <summary>
/// Exports every market, with its countries, currency, exchange rate and rounding, as a <c>Markets</c> recipe step.
/// </summary>
public sealed class MarketsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<MarketsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<MarketsDeploymentStep>("Markets");

    /// <inheritdoc/>
    public override string Name => MarketsConstants.Recipes.Markets;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
