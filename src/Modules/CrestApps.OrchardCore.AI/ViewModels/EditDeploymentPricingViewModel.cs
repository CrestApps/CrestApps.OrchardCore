using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Backs the pricing editor rendered on the AI deployment editor: what the deployment costs with its provider, used to
/// estimate the cost of its usage.
/// </summary>
public class EditDeploymentPricingViewModel
{
    /// <summary>
    /// Gets or sets the currency of <see cref="Prices"/>.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Gets or sets the prices, one per billed unit.
    /// </summary>
    public List<AIDeploymentPrice> Prices { get; set; } = [];
}
