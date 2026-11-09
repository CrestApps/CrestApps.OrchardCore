using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Marketplace.Deployments;

/// <summary>
/// Exports every marketplace vendor, with its users by name and its commission, as a <c>Vendors</c> recipe step. The vendor's Stripe Connect account stays on the site it was made on.
/// </summary>
public sealed class VendorsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<VendorsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<VendorsDeploymentStep>("Vendors");

    /// <inheritdoc/>
    public override string Name => MarketplaceConstants.Recipes.Vendors;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
