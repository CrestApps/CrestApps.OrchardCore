using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Imports the tax tables carried by the <c>TaxTable</c> recipe step, creating entries that do not exist and updating those that do.
/// </summary>
internal sealed class TaxTableStep : NamedCatalogRecipeStep<TaxTable>
{
    public TaxTableStep(INamedCatalogManager<TaxTable> manager)
        : base(TaxationDeploymentSteps.TaxTable, TaxationDeploymentSteps.TaxTableEntries, manager)
    {
    }
}
