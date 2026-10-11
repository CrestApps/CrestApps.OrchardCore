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

/// <summary>
/// Provides editor shapes and validation for tenant onboarding content parts.
/// </summary>
public sealed class TenantOnboardingPartDisplayDriver : ContentPartDisplayDriver<TenantOnboardingPart>
{
    private readonly ISetupService _setupService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantOnboardingPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="setupService">The setup service used to list available setup recipes.</param>
    /// <param name="stringLocalizer">The localizer used for validation messages.</param>
    public TenantOnboardingPartDisplayDriver(
        ISetupService setupService,
        IStringLocalizer<TenantOnboardingPartDisplayDriver> stringLocalizer)
    {
        _setupService = setupService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Builds the editor shape for selecting a setup recipe for tenant onboarding.
    /// </summary>
    /// <param name="part">The tenant onboarding content part.</param>
    /// <param name="context">The content part editor context.</param>
    /// <returns>The display result for the tenant onboarding editor.</returns>
    public override IDisplayResult Edit(TenantOnboardingPart part, BuildPartEditorContext context)
    {
        return Initialize<TenantOnboardingViewModel>(GetEditorShapeType(context), async model =>
        {
            var recipeCollections = await _setupService.GetSetupRecipesAsync();
            model.RecipeName = part.RecipeName;
            model.Recipes = recipeCollections.Select(x => new SelectListItem(x.DisplayName, x.Name)).ToArray();
        });
    }

    /// <summary>
    /// Updates the tenant onboarding content part from posted editor values and validates the selected recipe.
    /// </summary>
    /// <param name="part">The tenant onboarding content part to update.</param>
    /// <param name="context">The content part editor update context.</param>
    /// <returns>The updated editor display result.</returns>
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
