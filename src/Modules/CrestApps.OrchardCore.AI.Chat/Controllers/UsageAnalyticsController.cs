using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

/// <summary>
/// Provides endpoints for managing usage analytics resources.
/// </summary>
[Admin("AI/UsageAnalytics/{action}", "UsageAnalytics.{action}")]
public sealed class UsageAnalyticsController : Controller
{
    private readonly IAICompletionUsageService _usageService;
    private readonly IAIVoiceSessionSummaryStore _voiceSessionStore;
    private readonly IAIProfileStore _profileStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly IOptionsMonitor<GeneralAIOptions> _generalAIOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsageAnalyticsController"/> class.
    /// </summary>
    /// <param name="usageService">The usage service.</param>
    /// <param name="voiceSessionStore">The per-call AI voice session summaries.</param>
    /// <param name="profileStore">The AI profile store, for the profile filter and profile names.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="localClock">The local clock for timezone conversions.</param>
    /// <param name="clock">The clock, for converting call times to the site's time zone.</param>
    /// <param name="generalAIOptions">The general AI options.</param>
    public UsageAnalyticsController(
        IAICompletionUsageService usageService,
        IAIVoiceSessionSummaryStore voiceSessionStore,
        IAIProfileStore profileStore,
        IAuthorizationService authorizationService,
        ILocalClock localClock,
        IClock clock,
        IOptionsMonitor<GeneralAIOptions> generalAIOptions)
    {
        _usageService = usageService;
        _voiceSessionStore = voiceSessionStore;
        _profileStore = profileStore;
        _authorizationService = authorizationService;
        _localClock = localClock;
        _clock = clock;
        _generalAIOptions = generalAIOptions;
    }

    /// <summary>
    /// Performs the index operation.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChatAnalyticsPermissionProvider.ViewChatAnalytics))
        {
            return Forbid();
        }

        var model = new UsageAnalyticsIndexViewModel
        {
            IsAIUsageTrackingEnabled = _generalAIOptions.CurrentValue.EnableAIUsageTracking,
        };

        model.Profiles = ToSelectList(await GetProfileNamesAsync());

        return View(model);
    }

    /// <summary>
    /// Performs the index post operation.
    /// </summary>
    /// <param name="model">The model.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    public async Task<IActionResult> IndexPost(UsageAnalyticsIndexViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChatAnalyticsPermissionProvider.ViewChatAnalytics))
        {
            return Forbid();
        }

        model.IsAIUsageTrackingEnabled = _generalAIOptions.CurrentValue.EnableAIUsageTracking;

        var profileNames = await GetProfileNamesAsync();
        model.Profiles = ToSelectList(profileNames);

        // Convert local dates to UTC before querying.
        DateTime? startDateUtc = model.StartDate.HasValue
            ? await _localClock.ConvertToUtcAsync(model.StartDate.Value)
            : null;
        DateTime? endDateUtc = model.EndDate.HasValue
            ? await _localClock.ConvertToUtcAsync(model.EndDate.Value)
            : null;

        var records = await _usageService.GetAsync(startDateUtc, endDateUtc);
        var voiceSessions = await _voiceSessionStore.GetAsync(startDateUtc, endDateUtc);
        var timeZone = await _localClock.GetLocalTimeZoneAsync();

        ApplyReport(
            model,
            records,
            voiceSessions,
            profileNames,
            utc => _clock.ConvertToTimeZone(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), timeZone).DateTime);

        return View("Index", model);
    }

    private static void ApplyReport(
        UsageAnalyticsIndexViewModel model,
        IReadOnlyList<AICompletionUsageRecord> records,
        IReadOnlyList<AIVoiceSessionSummaryIndex> voiceSessions,
        IReadOnlyDictionary<string, string> profileNames,
        Func<DateTime, DateTime> toLocal)
    {
        model.ShowReport = true;

        var relevantRecords = AICompletionUsageReport.Relevant(records, model.ProfileId);

        model.TotalCalls = relevantRecords.Count;
        model.TotalSessions = relevantRecords
            .Select(record => record.SessionId)
            .Where(sessionId => !string.IsNullOrEmpty(sessionId))
            .Distinct(StringComparer.Ordinal)
            .Count();
        model.TotalChatInteractions = relevantRecords
            .Select(record => record.InteractionId)
            .Where(interactionId => !string.IsNullOrEmpty(interactionId))
            .Distinct(StringComparer.Ordinal)
            .Count();
        model.TotalTokens = relevantRecords.Sum(record => (long)record.TotalTokenCount);
        model.Rows = AICompletionUsageReport.BuildRows(relevantRecords, model.GroupBy, profileNames);

        // A call's text tokens are the completions recorded against its chat session: the turn-based replies and
        // the review that concludes every call.
        var textTokensBySession = AICompletionUsageReport.TokensBySession(relevantRecords);
        var calls = AIVoiceUsageReport.Calls(voiceSessions, model.ProfileId);

        model.VoiceTotals = AIVoiceUsageReport.Summarize(label: null, calls, textTokensBySession);
        model.VoiceRows = AIVoiceUsageReport.BuildRows(calls, model.VoiceGroupBy, profileNames, toLocal, textTokensBySession);
    }

    private async Task<IReadOnlyDictionary<string, string>> GetProfileNamesAsync()
        => (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Where(profile => !string.IsNullOrEmpty(profile.ItemId))
            .GroupBy(profile => profile.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => string.IsNullOrWhiteSpace(group.First().DisplayText) ? group.First().Name : group.First().DisplayText,
                StringComparer.Ordinal);

    private static List<SelectListItem> ToSelectList(IReadOnlyDictionary<string, string> profileNames)
        => profileNames
            .OrderBy(profile => profile.Value, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new SelectListItem(profile.Value, profile.Key))
            .ToList();
}
