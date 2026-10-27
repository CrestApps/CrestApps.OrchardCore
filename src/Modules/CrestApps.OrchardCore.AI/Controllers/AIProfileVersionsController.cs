using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// Lists the saved versions of an AI profile, compares a version with the profile as it is now or with another
/// version, and restores one.
/// </summary>
public sealed class AIProfileVersionsController : Controller
{
    private const int MaxComparableVersions = 100;

    private readonly IAIProfileManager _profileManager;
    private readonly IAIProfileVersionStore _versionStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileVersionsController"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="versionStore">The store the versions are kept in.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileVersionsController(
        IAIProfileManager profileManager,
        IAIProfileVersionStore versionStore,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<AIProfileVersionsController> htmlLocalizer,
        IStringLocalizer<AIProfileVersionsController> stringLocalizer)
    {
        _profileManager = profileManager;
        _versionStore = versionStore;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists a profile's versions, newest first.
    /// </summary>
    /// <param name="profileId">The profile.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("ai/profiles/{profileId}/versions", "AIProfileVersionsIndex")]
    public async Task<IActionResult> Index(
        string profileId,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var profile = await _profileManager.FindByIdAsync(profileId);

        if (profile is null)
        {
            return NotFound();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (versions, count) = await _versionStore.PageAsync(profile.ItemId, pager.Page, pager.PageSize);
        var current = CurrentVersion(profile);

        return View(new AIProfileVersionsViewModel
        {
            Profile = profile,
            CurrentVersion = current,
            List = new AdminListContext<AIProfileVersionEntry>
            {
                Source = versions.Select(version => new AIProfileVersionEntry(version, version.Version == current)).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, count, new Microsoft.AspNetCore.Routing.RouteData()),
            },
        });
    }

    /// <summary>
    /// Shows a version beside the profile as it is now, or beside another version of it.
    /// </summary>
    /// <param name="id">The version.</param>
    /// <param name="with">Another version of the same profile to show on the right, or <see langword="null"/> for the
    /// profile as it is now.</param>
    /// <param name="templateService">The template service, for the titles of the templates a profile uses.</param>
    [Admin("ai/profiles/versions/{id}", "AIProfileVersionsCompare")]
    public async Task<IActionResult> Compare(string id, string with, [FromServices] ITemplateService templateService)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var version = await _versionStore.FindByIdAsync(id);
        var profile = version is null ? null : await _profileManager.FindByIdAsync(version.ProfileId);

        if (profile is null || version.Snapshot is null)
        {
            return NotFound();
        }

        var other = string.IsNullOrEmpty(with) || with == id ? null : await _versionStore.FindByIdAsync(with);

        if (other is not null && (other.ProfileId != profile.ItemId || other.Snapshot is null))
        {
            other = null;
        }

        var templates = await templateService.ListAsync(HttpContext.RequestAborted) ?? [];
        var titles = templates
            .Where(template => !string.IsNullOrEmpty(template.Id))
            .DistinctBy(template => template.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(template => template.Id, AITemplateCategoryExtensions.GetTitle, StringComparer.OrdinalIgnoreCase);

        var (versions, _) = await _versionStore.PageAsync(profile.ItemId, 1, MaxComparableVersions);

        return View(new AIProfileVersionCompareViewModel
        {
            Profile = profile,
            Version = version,
            Other = other,
            CurrentVersion = CurrentVersion(profile),
            Versions = versions,
            Rows = AIProfileVersionComparer.Compare(version.Snapshot, other?.Snapshot ?? profile, titles),
        });
    }

    /// <summary>
    /// Restores a version: the profile takes its settings back, and the restore is saved as a new version.
    /// </summary>
    /// <param name="id">The version to restore.</param>
    [HttpPost]
    [Admin("ai/profiles/versions/{id}/restore", "AIProfileVersionsRestore")]
    public async Task<IActionResult> Restore(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var version = await _versionStore.FindByIdAsync(id);
        var profile = version is null ? null : await _profileManager.FindByIdAsync(version.ProfileId);

        if (profile is null || version.Snapshot is null)
        {
            return NotFound();
        }

        Restore(profile, version.Snapshot);

        var metadata = profile.GetOrCreate<AIProfileVersionMetadata>();
        metadata.PendingNote = S["Restored from version {0}", version.Version];
        profile.Put(metadata);

        await _profileManager.UpdateAsync(profile);

        await _notifier.SuccessAsync(H["Version {0} was restored as version {1}.", version.Version, CurrentVersion(profile)]);

        return RedirectToAction(nameof(Index), new { profileId = profile.ItemId });
    }

    /// <summary>
    /// Copies a version's settings onto the profile, keeping what identifies the profile: its identifier, its
    /// technical name, its type, its owner and when it was created.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="snapshot">The version's copy of the profile.</param>
    internal static void Restore(AIProfile profile, AIProfile snapshot)
    {
        var copy = snapshot.Clone();

        profile.DisplayText = copy.DisplayText;
        profile.Description = copy.Description;
        profile.OrchestratorName = copy.OrchestratorName;
        profile.ChatDeploymentName = copy.ChatDeploymentName;
        profile.UtilityDeploymentName = copy.UtilityDeploymentName;
        profile.TitleType = copy.TitleType;
        profile.WelcomeMessage = copy.WelcomeMessage;
        profile.PromptSubject = copy.PromptSubject;
        profile.PromptTemplate = copy.PromptTemplate;
        profile.Properties = copy.Properties;
        profile.Settings = copy.Settings;
    }

    private static int CurrentVersion(AIProfile profile)
        => profile.TryGet<AIProfileVersionMetadata>(out var metadata) ? metadata.Version : 0;
}
