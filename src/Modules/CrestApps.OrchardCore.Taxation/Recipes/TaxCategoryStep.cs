using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Imports the tax categories carried by the <c>TaxCategory</c> recipe step, creating entries that do not exist and updating those that do.
/// </summary>
internal sealed class TaxCategoryStep : NamedCatalogRecipeStep<TaxCategory>
{
    public TaxCategoryStep(INamedCatalogManager<TaxCategory> manager)
        : base(TaxationDeploymentSteps.TaxCategory, TaxationDeploymentSteps.TaxCategoryEntries, manager)
    {
    }
}
