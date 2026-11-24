using CrestApps.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Recipes;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.Agreements.Deployments;

/// <summary>
/// Writes every agreement into a deployment plan's recipe.
/// </summary>
internal sealed class AgreementsDeploymentSource : CatalogDeploymentSource<AgreementsDeploymentStep, Agreement>
{
    public AgreementsDeploymentSource(ICatalogManager<Agreement> manager)
        : base(AgreementsRecipeStep.EntriesName, manager)
    {
    }
}
