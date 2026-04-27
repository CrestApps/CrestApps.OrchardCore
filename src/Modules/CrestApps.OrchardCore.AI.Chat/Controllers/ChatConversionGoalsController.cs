using System.Text;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

/// <summary>
/// Provides endpoints for managing chat conversion goals resources.
/// </summary>
[Admin("AI/ChatConversionGoals/{action}", "ChatConversionGoals.{action}")]
public sealed class ChatConversionGoalsController : Controller
{
    private readonly IAIProfileManager _profileManager;
    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatConversionGoalsController"/> class.
    /// </summary>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="session">The session.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="clock">The clock.</param>
    public ChatConversionGoalsController(
        IAIProfileManager profileManager,
        ISession session,
        IAuthorizationService authorizationService,
        IClock clock)
    {
        _profileManager = profileManager;
        _session = session;
        _authorizationService = authorizationService;
        _clock = clock;
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

        return View(await BuildViewModelAsync(new ChatConversionGoalsIndexViewModel(), false));
    }

    /// <summary>
    /// Performs the index post operation.
    /// </summary>
    /// <param name="model">The model.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    public async Task<IActionResult> IndexPost(ChatConversionGoalsIndexViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChatAnalyticsPermissionProvider.ViewChatAnalytics))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(model.ProfileId))
        {
            ModelState.AddModelError(nameof(model.ProfileId), "AI Profile is required.");
        }

        model = await BuildViewModelAsync(model, ModelState.IsValid);

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var events = await GetEventsAsync(model);
        ApplyReport(model, events);

        return View("Index", model);
    }

    /// <summary>
    /// Performs the export operation.
    /// </summary>
    /// <param name="model">The model.</param>
    [HttpPost]
    public async Task<IActionResult> Export(ChatConversionGoalsIndexViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChatAnalyticsPermissionProvider.ExportChatAnalytics))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(model.ProfileId))
        {
            return BadRequest();
        }

        var rows = BuildRows(await GetEventsAsync(model));
        var columns = rows
            .SelectMany(row => row.Values.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return File(
            Encoding.UTF8.GetBytes(GenerateCsv(rows, columns)),
            "text/csv",
            $"chat-conversion-goals-{_clock.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private async Task<ChatConversionGoalsIndexViewModel> BuildViewModelAsync(ChatConversionGoalsIndexViewModel model, bool showReport)
    {
        var profiles = await _profileManager.GetAsync(AIProfileType.Chat);

        model.Profiles =
        [
            new SelectListItem("Select a profile", string.Empty, string.IsNullOrEmpty(model.ProfileId)),
            .. profiles
                .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
                .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId, profile.ItemId == model.ProfileId)),
        ];
        model.ShowReport = showReport;

        return model;
    }

    private async Task<IReadOnlyList<AIChatSessionEvent>> GetEventsAsync(ChatConversionGoalsIndexViewModel model)
    {
        var query = _session.Query<AIChatSessionEvent, AIChatSessionMetricsIndex>(
            index => index.ProfileId == model.ProfileId,
            collection: AIConstants.AICollectionName);

        if (model.StartDateUtc.HasValue)
        {
            query = query.Where(index => index.SessionStartedUtc >= model.StartDateUtc.Value);
        }

        if (model.EndDateUtc.HasValue)
        {
            var end = model.EndDateUtc.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(index => index.SessionStartedUtc <= end);
        }

        return (await query.ListAsync()).ToList();
    }

    private static void ApplyReport(ChatConversionGoalsIndexViewModel model, IReadOnlyList<AIChatSessionEvent> events)
    {
        var rows = BuildRows(events);
        model.Rows = rows;
        model.Columns = rows
            .SelectMany(row => row.Values.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static List<ChatConversionGoalsRowViewModel> BuildRows(IReadOnlyList<AIChatSessionEvent> events) =>
        events
            .Where(evt => evt.ConversionGoalResults.Count > 0 || evt.ConversionScore.HasValue)
            .OrderByDescending(evt => evt.SessionStartedUtc)
            .Select(evt => new ChatConversionGoalsRowViewModel
            {
                SessionId = evt.SessionId,
                SessionStartedUtc = evt.SessionStartedUtc,
                TotalPoints = FormatPoints(evt.ConversionScore, evt.ConversionMaxScore),
                Values = evt.ConversionGoalResults.ToDictionary(
                    result => result.Name,
                    result => FormatPoints(result.Score, result.MaxScore),
                    StringComparer.OrdinalIgnoreCase),
            })
            .ToList();

    private static string GenerateCsv(
        IReadOnlyList<ChatConversionGoalsRowViewModel> rows,
        IReadOnlyList<string> columns)
    {
        var builder = new StringBuilder();
        builder.Append("SessionStartedUtc,SessionId,TotalPoints");

        foreach (var column in columns)
        {
            builder.Append(',').Append(EscapeCsv(column));
        }

        builder.AppendLine();

        foreach (var row in rows)
        {
            builder.Append(row.SessionStartedUtc.ToString("o")).Append(',')
                .Append(EscapeCsv(row.SessionId)).Append(',')
                .Append(EscapeCsv(row.TotalPoints));

            foreach (var column in columns)
            {
                row.Values.TryGetValue(column, out var value);
                builder.Append(',').Append(EscapeCsv(value));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatPoints(int? score, int? maxScore)
    {
        if (!score.HasValue)
        {
            return string.Empty;
        }

        return maxScore.HasValue && maxScore.Value > 0
            ? $"{score.Value} / {maxScore.Value}"
            : score.Value.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
