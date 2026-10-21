using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Imports the tax jurisdictions carried by the <c>TaxJurisdiction</c> recipe step, creating entries that do not exist and updating those that do.
/// </summary>
internal sealed class TaxJurisdictionStep : NamedCatalogRecipeStep<TaxJurisdiction>
{
    public TaxJurisdictionStep(INamedCatalogManager<TaxJurisdiction> manager)
        : base(TaxationDeploymentSteps.TaxJurisdiction, TaxationDeploymentSteps.TaxJurisdictionEntries, manager)
    {
    }
}
