using CrestApps.OrchardCore.Customers.Core;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Exports every customer's store credit, with its balance and activity, as a <c>StoreCreditAccounts</c> recipe step.
/// </summary>
public sealed class StoreCreditAccountsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<StoreCreditAccountsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<StoreCreditAccountsDeploymentStep>("Store credit");

    /// <inheritdoc/>
    public override string Name => CustomersConstants.Recipes.StoreCreditAccounts;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
