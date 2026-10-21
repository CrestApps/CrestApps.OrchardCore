using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Imports the tax types carried by the <c>TaxType</c> recipe step, creating entries that do not exist and updating those that do.
/// </summary>
internal sealed class TaxTypeStep : NamedCatalogRecipeStep<TaxType>
{
    public TaxTypeStep(INamedCatalogManager<TaxType> manager)
        : base(TaxationDeploymentSteps.TaxType, TaxationDeploymentSteps.TaxTypeEntries, manager)
    {
    }
}
