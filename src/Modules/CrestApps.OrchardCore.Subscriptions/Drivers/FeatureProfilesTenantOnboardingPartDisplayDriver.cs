using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class FeatureProfilesTenantOnboardingPartDisplayDriver : ContentPartDisplayDriver<TenantOnboardingPart>
{
    private readonly IFeatureProfilesService _featureProfilesService;

    internal readonly IStringLocalizer S;

    public FeatureProfilesTenantOnboardingPartDisplayDriver(
        IFeatureProfilesService featureProfilesService,
        IStringLocalizer<TenantOnboardingPartDisplayDriver> stringLocalizer)
    {
        _featureProfilesService = featureProfilesService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TenantOnboardingPart part, BuildPartEditorContext context)
    {
        return Initialize<FeatureProfilesViewModel>(GetEditorShapeType(context), async model =>
        {
            var profiles = await _featureProfilesService.GetFeatureProfilesAsync();

            model.FeatureProfiles = profiles.Values
            .Select(profile => new SelectListItem(profile.Name, profile.Id))
            .ToArray();
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(TenantOnboardingPart part, UpdatePartEditorContext context)
    {
        var model = new FeatureProfilesViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!string.IsNullOrEmpty(model.FeatureProfile))
        {
            var profiles = await _featureProfilesService.GetFeatureProfilesAsync();

            if (!profiles.Values.Any(recipe => recipe.Id == model.FeatureProfile))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.FeatureProfile), S["Invalid Features Profile."]);
            }
        }

        part.FeatureProfile = model.FeatureProfile;

        return Edit(part, context);
    }
}
