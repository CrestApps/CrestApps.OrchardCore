using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Products.Deployments;

/// <summary>
/// Represents a deployment step that exports managed currencies.
/// </summary>
public sealed class CurrencyDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<CurrencyDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<CurrencyDeploymentStep>("Currencies");

    /// <inheritdoc />
    public override string Name => ProductsConstants.Recipes.Currencies;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets a value indicating whether all currencies should be exported.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the selected currency identifiers.
    /// </summary>
    public string[] CurrencyIds { get; set; }
}
