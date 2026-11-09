using CrestApps.OrchardCore.Customers.Core;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Exports every customer's loyalty points, with their activity, as a <c>LoyaltyAccounts</c> recipe step.
/// </summary>
public sealed class LoyaltyAccountsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LoyaltyAccountsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LoyaltyAccountsDeploymentStep>("Loyalty points");

    /// <inheritdoc/>
    public override string Name => CustomersConstants.Recipes.LoyaltyAccounts;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
