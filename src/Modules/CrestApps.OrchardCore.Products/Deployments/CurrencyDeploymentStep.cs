using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Products.Deployments;

/// <summary>
/// Represents a deployment step that exports managed currencies.
/// </summary>
public sealed class CurrencyDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyDeploymentStep"/> class.
    /// </summary>
    public CurrencyDeploymentStep()
    {
        Name = ProductsConstants.Recipes.Currencies;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CurrencyDeploymentStep(IStringLocalizer<CurrencyDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Commerce"];
    }

    /// <summary>
    /// Gets or sets a value indicating whether all currencies should be exported.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the selected currency identifiers.
    /// </summary>
    public string[] CurrencyIds { get; set; }
}
