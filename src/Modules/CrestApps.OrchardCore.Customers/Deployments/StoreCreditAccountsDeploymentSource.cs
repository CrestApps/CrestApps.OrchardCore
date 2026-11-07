using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Recipes;
using CrestApps.OrchardCore.Customers.Services;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Writes every store credit account into a <c>StoreCreditAccounts</c> recipe step, keyed by its id and its customer and currency.
/// </summary>
internal sealed class StoreCreditAccountsDeploymentSource : CatalogStoreDeploymentSource<StoreCreditAccountsDeploymentStep, StoreCreditAccount>
{
    public StoreCreditAccountsDeploymentSource(IStoreCreditStore store)
        : base(StoreCreditAccountsStep.EntriesName, store)
    {
    }
}
