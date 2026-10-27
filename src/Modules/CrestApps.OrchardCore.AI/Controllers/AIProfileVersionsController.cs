using System.Globalization;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
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
/// Lists the saved versions of an AI profile, compares a version with the profile as it is now, and restores one.
/// </summary>
public sealed class AIProfileVersionsController : Controller
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerOptions.Web) { WriteIndented = true };

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
    /// Shows a version beside the profile as it is now.
    /// </summary>
    /// <param name="id">The version.</param>
    [Admin("ai/profiles/versions/{id}", "AIProfileVersionsCompare")]
    public async Task<IActionResult> Compare(string id)
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

        return View(new AIProfileVersionCompareViewModel
        {
            Profile = profile,
            Version = version,
            CurrentVersion = CurrentVersion(profile),
            Rows = Compare(version.Snapshot, profile),
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

    /// <summary>
    /// Lists what a version and the profile hold, side by side: the main fields first, then every other setting
    /// that differs.
    /// </summary>
    /// <param name="then">The version's copy of the profile.</param>
    /// <param name="now">The profile as it is now.</param>
    internal List<AIProfileVersionCompareRow> Compare(AIProfile then, AIProfile now)
    {
        var thenMetadata = then.TryGet<AIProfileMetadata>(out var thenSettings) ? thenSettings : new AIProfileMetadata();
        var nowMetadata = now.TryGet<AIProfileMetadata>(out var nowSettings) ? nowSettings : new AIProfileMetadata();

        var rows = new List<AIProfileVersionCompareRow>
        {
            Row(S["Name"], then.DisplayText, now.DisplayText),
            Row(S["Description"], then.Description, now.Description),
            Row(S["Chat deployment"], then.ChatDeploymentName, now.ChatDeploymentName),
            Row(S["Utility deployment"], then.UtilityDeploymentName, now.UtilityDeploymentName),
            Row(S["Orchestrator"], then.OrchestratorName, now.OrchestratorName),
            Row(S["Welcome message"], then.WelcomeMessage, now.WelcomeMessage),
            Row(S["System message"], thenMetadata.SystemMessage, nowMetadata.SystemMessage, true),
            Row(S["Initial prompt"], thenMetadata.InitialPrompt, nowMetadata.InitialPrompt, true),
            Row(S["Temperature"], Format(thenMetadata.Temperature), Format(nowMetadata.Temperature)),
            Row(S["Top P"], Format(thenMetadata.TopP), Format(nowMetadata.TopP)),
            Row(S["Max tokens"], Format(thenMetadata.MaxTokens), Format(nowMetadata.MaxTokens)),
            Row(S["Past messages"], Format(thenMetadata.PastMessagesCount), Format(nowMetadata.PastMessagesCount)),
        };

        AddOtherSettings(rows, then.Properties, now.Properties);
        AddOtherSettings(rows, then.Settings, now.Settings);

        return rows;
    }

    private static void AddOtherSettings(List<AIProfileVersionCompareRow> rows, IDictionary<string, object> then, IDictionary<string, object> now)
    {
        then ??= new Dictionary<string, object>();
        now ??= new Dictionary<string, object>();

        foreach (var key in then.Keys.Union(now.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
        {
            // Shown above, or changes on every save.
            if (key is nameof(AIProfileMetadata) or nameof(AIProfileVersionMetadata))
            {
                continue;
            }

            var thenJson = then.TryGetValue(key, out var thenValue) ? ToJson(thenValue) : null;
            var nowJson = now.TryGetValue(key, out var nowValue) ? ToJson(nowValue) : null;

            if (!string.Equals(thenJson, nowJson, StringComparison.Ordinal))
            {
                rows.Add(new AIProfileVersionCompareRow(Humanize(key), thenJson, nowJson, true, IsCode: true));
            }
        }
    }

    private static AIProfileVersionCompareRow Row(string label, string then, string now, bool multiline = false)
        => new(label, then, now, multiline);

    private static string Format(float? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    private static string Format(int? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    private static string ToJson(object value)
        => value is null ? null : JsonSerializer.Serialize(value, _jsonOptions);

    // "AIProfileDataExtractionSettings" reads better as "AI Profile Data Extraction Settings".
    private static string Humanize(string key)
    {
        var builder = new System.Text.StringBuilder(key.Length + 8);

        for (var i = 0; i < key.Length; i++)
        {
            if (i > 0 && char.IsUpper(key[i]) && (char.IsLower(key[i - 1]) || (i + 1 < key.Length && char.IsLower(key[i + 1]) && char.IsUpper(key[i - 1]))))
            {
                builder.Append(' ');
            }

            builder.Append(key[i]);
        }

        return builder.ToString();
    }

    private static int CurrentVersion(AIProfile profile)
        => profile.TryGet<AIProfileVersionMetadata>(out var metadata) ? metadata.Version : 0;
}
