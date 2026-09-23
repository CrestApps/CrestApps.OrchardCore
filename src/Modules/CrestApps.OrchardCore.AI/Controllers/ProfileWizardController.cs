using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// Provides the "New AI profile" wizard: pick a starting point, answer the few questions it needs, and land in
/// the profile editor with a profile that already exists.
/// </summary>
/// <remarks>
/// The starting points are picked from a modal on the AI profiles list. The services this controller shares
/// with that list are internal to the module, and a controller has to be public with a public constructor, so
/// they are resolved per request rather than injected.
/// </remarks>
public sealed class ProfileWizardController : Controller
{
    /// <summary>
    /// The fragment that opens the "New AI profile" picker when the AI profiles list loads.
    /// </summary>
    public const string PickerFragment = "new-profile";

    private readonly IAIProfileManager _profileManager;
    private readonly IAIProfileTemplateManager _templateManager;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileWizardController"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="templateManager">The AI profile template manager.</param>
    /// <param name="deploymentManager">The AI deployment manager.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier service.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProfileWizardController(
        IAIProfileManager profileManager,
        IAIProfileTemplateManager templateManager,
        IAIDeploymentManager deploymentManager,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<ProfileWizardController> htmlLocalizer,
        IStringLocalizer<ProfileWizardController> stringLocalizer)
    {
        _profileManager = profileManager;
        _templateManager = templateManager;
        _deploymentManager = deploymentManager;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Opens the AI profiles list with the "New AI profile" picker showing.
    /// </summary>
    /// <returns>A redirect to the AI profiles list.</returns>
    [Admin("ai/profile/new", "AIProfilesNew")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        return RedirectToPicker();
    }

    /// <summary>
    /// Displays the setup step for the chosen starting point.
    /// </summary>
    /// <param name="templateId">The identifier of the template the profile is created from.</param>
    /// <returns>The setup view, or a redirect to the AI profiles list when the template cannot be used.</returns>
    [Admin("ai/profile/new/{templateId}", "AIProfilesNewFromTemplate")]
    public async Task<IActionResult> Setup(string templateId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var (template, scenario, unavailableResult) = await FindUsableTemplateAsync(templateId);

        if (unavailableResult is not null)
        {
            return unavailableResult;
        }

        var model = new ProfileWizardSetupViewModel
        {
            DisplayText = scenario.Title,
        };

        await PopulateSetupAsync(model, template, scenario, preselectTemplateDeployment: true);

        return View(model);
    }

    /// <summary>
    /// Creates the profile for the chosen starting point and opens it in the profile editor.
    /// </summary>
    /// <param name="templateId">The identifier of the template the profile is created from.</param>
    /// <returns>A redirect to the profile editor on success, or the setup view with validation errors.</returns>
    [HttpPost]
    [ActionName(nameof(Setup))]
    [Admin("ai/profile/new/{templateId}", "AIProfilesNewFromTemplate")]
    public async Task<IActionResult> SetupPost(string templateId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var (template, scenario, unavailableResult) = await FindUsableTemplateAsync(templateId);

        if (unavailableResult is not null)
        {
            return unavailableResult;
        }

        var model = new ProfileWizardSetupViewModel();

        await TryUpdateModelAsync(model);

        var profileFactory = HttpContext.RequestServices.GetRequiredService<AIProfileTemplateProfileFactory>();

        var profile = await profileFactory.NewFromTemplateAsync(template);

        if (profile is null)
        {
            await _notifier.ErrorAsync(H["Unable to create a new profile."]);

            return RedirectToAction(nameof(ProfilesController.Index), "Profiles");
        }

        profile.Name = model.Name?.Trim();
        profile.DisplayText = model.DisplayText?.Trim();
        profile.ChatDeploymentName = string.IsNullOrWhiteSpace(model.ChatDeploymentName)
            ? null
            : model.ChatDeploymentName.Trim();

        var validation = await _profileManager.ValidateAsync(profile);

        foreach (var error in validation.Errors)
        {
            var key = GetModelKey(error.MemberNames.FirstOrDefault());

            // The profile manager calls a missing name "Name"; the field is labelled "Technical name", so that
            // one error is worded here instead, the same way the profile editor words it.
            if (key == nameof(model.Name) && string.IsNullOrEmpty(profile.Name))
            {
                continue;
            }

            ModelState.AddModelError(key, error.ErrorMessage);
        }

        if (string.IsNullOrEmpty(profile.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["Technical name is required."]);
        }

        // The profile editor refuses a profile without a title; the profile manager does not.
        if (string.IsNullOrEmpty(profile.DisplayText))
        {
            ModelState.AddModelError(nameof(model.DisplayText), S["Title is required."]);
        }

        if (!ModelState.IsValid)
        {
            await PopulateSetupAsync(model, template, scenario, preselectTemplateDeployment: false);

            return View(model);
        }

        await profileFactory.CreateAsync(profile, template);

        await _notifier.SuccessAsync(H["Created from <em>{0}</em>. Everything below is optional.", scenario.Title]);

        return RedirectToAction(nameof(ProfilesController.Edit), "Profiles", new { id = profile.ItemId });
    }

    /// <summary>
    /// Finds the template for a starting point and makes sure it can be used on this tenant.
    /// </summary>
    /// <returns>
    /// The template and its starting point, or a redirect to the AI profiles list when the template does not
    /// exist, is not a profile template, or needs a feature that is not enabled.
    /// </returns>
    private async Task<(AIProfileTemplate Template, ProfileScenarioCardViewModel Scenario, IActionResult UnavailableResult)> FindUsableTemplateAsync(string templateId)
    {
        var template = string.IsNullOrWhiteSpace(templateId)
            ? null
            : await _templateManager.FindByIdAsync(templateId);

        if (template is null || !string.Equals(template.Source, AITemplateSources.Profile, StringComparison.OrdinalIgnoreCase))
        {
            await _notifier.WarningAsync(H["The selected starting point could not be found."]);

            return (null, null, RedirectToAction(nameof(ProfilesController.Index), "Profiles"));
        }

        var scenario = await HttpContext.RequestServices.GetRequiredService<ProfileScenarioCatalog>().GetScenarioAsync(template);

        if (!scenario.IsAvailable)
        {
            await _notifier.WarningAsync(H["Enable {0} to use <em>{1}</em>.", string.Join(", ", scenario.MissingFeatureNames), scenario.Title]);

            return (null, null, RedirectToAction(nameof(ProfilesController.Index), "Profiles"));
        }

        return (template, scenario, null);
    }

    private async Task PopulateSetupAsync(
        ProfileWizardSetupViewModel model,
        AIProfileTemplate template,
        ProfileScenarioCardViewModel scenario,
        bool preselectTemplateDeployment)
    {
        // The editor offers the chat slot only, so the wizard does too.
        var chatDeployments = (await _deploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.Chat)).ToList();

        model.Scenario = scenario;
        model.ChatDeployments = chatDeployments.ToSelectList(S["Standalone"].Value);

        if (preselectTemplateDeployment)
        {
            // A template written for another tenant can name a deployment this one does not have. Preselecting
            // it would only earn a validation error, so the site default is left in place instead.
            var templateDeploymentName = template.GetOrCreate<ProfileTemplateMetadata>().ChatDeploymentName;

            model.ChatDeploymentName = string.IsNullOrWhiteSpace(templateDeploymentName)
                ? null
                : chatDeployments.FirstOrDefault(deployment =>
                    string.Equals(deployment.Name, templateDeploymentName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(deployment.ItemId, templateDeploymentName, StringComparison.Ordinal))?.Name;
        }

        var settings = await _siteService.GetSettingsAsync<DefaultAIDeploymentSettings>();
        model.ShowMissingDefaultChatDeploymentWarning = string.IsNullOrEmpty(settings.DefaultChatDeploymentName);
    }

    private RedirectResult RedirectToPicker()
        => Redirect(Url.Action(nameof(ProfilesController.Index), "Profiles") + "#" + PickerFragment);

    /// <summary>
    /// Maps a profile property named by a validation error to the setup field that holds it, so the error
    /// shows next to that field. An error about anything the setup step does not ask for goes to the summary.
    /// </summary>
    private static string GetModelKey(string memberName)
        => memberName switch
        {
            nameof(AIProfile.Name) => nameof(ProfileWizardSetupViewModel.Name),
            nameof(AIProfile.DisplayText) => nameof(ProfileWizardSetupViewModel.DisplayText),
            nameof(AIProfile.ChatDeploymentName) => nameof(ProfileWizardSetupViewModel.ChatDeploymentName),
            _ => string.Empty,
        };
}
