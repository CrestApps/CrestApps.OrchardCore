using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.SiteResources.Models;

namespace CrestApps.OrchardCore.SiteResources.Recipes;

internal sealed class SiteResourcesStep : CatalogRecipeStep<SiteResource>
{
    public SiteResourcesStep(ICatalogManager<SiteResource> manager)
        : base(SiteResourcesConstants.RecipeStepName, "Resources", manager)
    {
    }
}
