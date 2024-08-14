using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Setup.Services;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class TenantOnboardingPartDisplayDriver : ContentPartDisplayDriver<TenantOnboardingPart>
{
    private readonly ISetupService _setupService;

    internal readonly IStringLocalizer S;

    public TenantOnboardingPartDisplayDriver(
        ISetupService setupService,
        IStringLocalizer<TenantOnboardingPartDisplayDriver> stringLocalizer)
    {
        _setupService = setupService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TenantOnboardingPart part, BuildPartEditorContext context)
    {
        return Initialize<TenantOnboardingViewModel>(GetEditorShapeType(context), async model =>
        {
            var recipeCollections = await _setupService.GetSetupRecipesAsync();

            model.Recipes = recipeCollections.Select(x => new SelectListItem(x.DisplayName, x.Name)).ToArray();
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(TenantOnboardingPart part, UpdatePartEditorContext context)
    {
        var model = new TenantOnboardingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.RecipeName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RecipeName), S["Recipe is required."]);
        }
        else
        {
            var recipeCollections = await _setupService.GetSetupRecipesAsync();

            if (!recipeCollections.Any(recipe => recipe.Name == model.RecipeName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.RecipeName), S["Invalid recipe name."]);
            }
        }

        part.RecipeName = model.RecipeName;

        return Edit(part, context);
    }
}
