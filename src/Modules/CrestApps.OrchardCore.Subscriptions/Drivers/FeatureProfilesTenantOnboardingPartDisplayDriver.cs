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

/// <summary>
/// Adds feature profile selection to the tenant onboarding part editor.
/// </summary>
public sealed class FeatureProfilesTenantOnboardingPartDisplayDriver : ContentPartDisplayDriver<TenantOnboardingPart>
{
    private readonly IFeatureProfilesService _featureProfilesService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureProfilesTenantOnboardingPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="featureProfilesService">The service used to load available feature profiles.</param>
    /// <param name="stringLocalizer">The string localizer for validation messages.</param>
    public FeatureProfilesTenantOnboardingPartDisplayDriver(
        IFeatureProfilesService featureProfilesService,
        IStringLocalizer<TenantOnboardingPartDisplayDriver> stringLocalizer)
    {
        _featureProfilesService = featureProfilesService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Builds the tenant onboarding part editor with the available feature profiles.
    /// </summary>
    /// <param name="part">The tenant onboarding part being edited.</param>
    /// <param name="context">The content part editor build context.</param>
    /// <returns>The editor display result.</returns>
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

    /// <summary>
    /// Updates the tenant onboarding part with the selected feature profile.
    /// </summary>
    /// <param name="part">The tenant onboarding part being updated.</param>
    /// <param name="context">The content part editor update context.</param>
    /// <returns>The updated editor display result.</returns>
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
