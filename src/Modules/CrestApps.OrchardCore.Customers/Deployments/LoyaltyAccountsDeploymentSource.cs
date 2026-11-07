using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Recipes;
using CrestApps.OrchardCore.Customers.Services;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Writes every loyalty account into a <c>LoyaltyAccounts</c> recipe step, keyed by its id and its customer.
/// </summary>
internal sealed class LoyaltyAccountsDeploymentSource : CatalogStoreDeploymentSource<LoyaltyAccountsDeploymentStep, LoyaltyAccount>
{
    public LoyaltyAccountsDeploymentSource(ILoyaltyStore store)
        : base(LoyaltyAccountsStep.EntriesName, store)
    {
    }
}
