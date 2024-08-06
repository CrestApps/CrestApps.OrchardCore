using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class TenantOnboardingPartDisplayDriver : ContentPartDisplayDriver<TenantOnboardingPart>
{
    private readonly IEnumerable<IRecipeHarvester> _recipeHarvesters;

    internal readonly IStringLocalizer S;

    public TenantOnboardingPartDisplayDriver(
        IEnumerable<IRecipeHarvester> recipeHarvesters,
        IStringLocalizer<TenantOnboardingPartDisplayDriver> stringLocalizer)
    {
        _recipeHarvesters = recipeHarvesters;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TenantOnboardingPart part, BuildPartEditorContext context)
    {
        return Initialize<TenantOnboardingViewModel>(GetEditorShapeType(context), async model =>
        {
            var recipeCollections = await Task.WhenAll(_recipeHarvesters.Select(harvester => harvester.HarvestRecipesAsync()));
            model.Recipes = recipeCollections.SelectMany(recipe => recipe)
            .Where(x => x.IsSetupRecipe)
            .Select(x => new SelectListItem(x.DisplayName, x.Name))
            .ToArray();
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(TenantOnboardingPart part, IUpdateModel updater, UpdatePartEditorContext context)
    {
        var model = new TenantOnboardingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.RecipeName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RecipeName), S["Recipe is required."]);
        }
        else
        {
            var recipeCollections = await Task.WhenAll(_recipeHarvesters.Select(harvester => harvester.HarvestRecipesAsync()));

            if (!recipeCollections.Any(recipes => recipes.Any(recipe => recipe.IsSetupRecipe && recipe.Name == model.RecipeName)))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.RecipeName), S["Invalid recipe."]);
            }
        }

        part.RecipeName = model.RecipeName;

        return Edit(part, context);
    }
}
