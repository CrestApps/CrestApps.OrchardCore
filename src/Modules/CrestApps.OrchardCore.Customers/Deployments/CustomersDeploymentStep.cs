using CrestApps.OrchardCore.Customers.Core;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Exports the guest and business customers, with their addresses, as a <c>Customers</c> recipe step. Customers with a user account are made from their user on each site, so they are left out, as are erased and merged customers.
/// </summary>
public sealed class CustomersDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<CustomersDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<CustomersDeploymentStep>("Customers");

    /// <inheritdoc/>
    public override string Name => CustomersConstants.Recipes.Customers;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
