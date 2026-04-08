using System.Text;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Services;
using CrestApps.Core.Mvc.Web.Areas.AIChat.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Controllers;

[Area("AIChat")]
[Authorize(Policy = "Admin")]
public sealed class ChatAnalyticsController : Controller
{
    private readonly IAIProfileManager _profileManager;
    private readonly MvcAIChatSessionEventService _eventService;
    private readonly TimeProvider _timeProvider;

    public ChatAnalyticsController(
        IAIProfileManager profileManager,
        MvcAIChatSessionEventService eventService,
        TimeProvider timeProvider)
    {
        _profileManager = profileManager;
        _eventService = eventService;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(await BuildViewModelAsync(new ChatAnalyticsIndexViewModel(), showReport: false));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Index))]
    public async Task<IActionResult> IndexPost(ChatAnalyticsIndexViewModel model)
    {
        var events = await _eventService.GetAsync(model.ProfileId, model.StartDateUtc, model.EndDateUtc);
        model = await BuildViewModelAsync(model, showReport: true);
        ApplyAnalytics(model, events);

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(ChatAnalyticsIndexViewModel model)
    {
        var events = await _eventService.GetAsync(model.ProfileId, model.StartDateUtc, model.EndDateUtc);
        var fileName = $"chat-analytics-{_timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.csv";

        return File(Encoding.UTF8.GetBytes(GenerateCsv(events)), "text/csv", fileName);
    }

    private async Task<ChatAnalyticsIndexViewModel> BuildViewModelAsync(ChatAnalyticsIndexViewModel model, bool showReport)
    {
        var profiles = await _profileManager.GetAsync(AIProfileType.Chat);

        model.Profiles =
        [
            new SelectListItem("All profiles", string.Empty, string.IsNullOrEmpty(model.ProfileId)),
            .. profiles
                .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
                .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId, profile.ItemId == model.ProfileId)),
        ];
        model.ShowReport = showReport;

        return model;
    }

    private static void ApplyAnalytics(ChatAnalyticsIndexViewModel model, IReadOnlyList<AIChatSessionEvent> events)
    {
        model.TotalSessions = events.Count;
        model.UniqueVisitors = events
            .Where(evt => !string.IsNullOrEmpty(evt.VisitorId))
            .Select(evt => evt.VisitorId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        model.ResolvedSessions = events.Count(evt => evt.IsResolved);
        model.AbandonedSessions = events.Count(evt => !evt.IsResolved && evt.SessionEndedUtc.HasValue);
        model.ActiveSessions = events.Count(evt => !evt.SessionEndedUtc.HasValue);

        if (model.TotalSessions > 0)
        {
            model.ContainmentRatePercent = Math.Round((double)model.ResolvedSessions / model.TotalSessions * 100, 1);
            model.AbandonmentRatePercent = Math.Round((double)model.AbandonedSessions / model.TotalSessions * 100, 1);
            model.AverageMessagesPerSession = Math.Round(events.Average(evt => evt.MessageCount), 1);

            var sessionsWithHandleTime = events.Where(evt => evt.HandleTimeSeconds > 0).ToList();
            model.AverageHandleTimeSeconds = sessionsWithHandleTime.Count > 0
                ? Math.Round(sessionsWithHandleTime.Average(evt => evt.HandleTimeSeconds), 1)
                : 0;

            var visitorSessionCounts = events
                .Where(evt => !string.IsNullOrEmpty(evt.VisitorId))
                .GroupBy(evt => evt.VisitorId)
                .ToList();

            if (visitorSessionCounts.Count > 0)
            {
                var returningVisitors = visitorSessionCounts.Count(group => group.Count() > 1);
                model.ReturningUserRatePercent = Math.Round((double)returningVisitors / visitorSessionCounts.Count * 100, 1);
            }

            var resolvedEvents = events.Where(evt => evt.IsResolved).ToList();

            if (resolvedEvents.Count > 0)
            {
                model.AverageStepsToResolution = Math.Round(resolvedEvents.Average(evt => evt.MessageCount), 1);
            }
        }

        var eventsWithLatency = events.Where(evt => evt.AverageResponseLatencyMs > 0).ToList();
        var eventsWithTokens = events.Where(evt => evt.TotalInputTokens > 0 || evt.TotalOutputTokens > 0).ToList();
        model.SessionsWithLatencyData = eventsWithLatency.Count;
        model.SessionsWithTokenData = eventsWithTokens.Count;
        model.HasPerformanceData = eventsWithLatency.Count > 0 || eventsWithTokens.Count > 0;

        if (eventsWithLatency.Count > 0)
        {
            model.AverageResponseLatencyMs = Math.Round(eventsWithLatency.Average(evt => evt.AverageResponseLatencyMs), 0);
        }

        if (eventsWithTokens.Count > 0)
        {
            model.TotalInputTokens = eventsWithTokens.Sum(evt => (long)evt.TotalInputTokens);
            model.TotalOutputTokens = eventsWithTokens.Sum(evt => (long)evt.TotalOutputTokens);
            model.TotalTokens = model.TotalInputTokens + model.TotalOutputTokens;
            model.AverageTokensPerSession = Math.Round((double)model.TotalTokens / eventsWithTokens.Count, 0);
            model.AverageInputTokensPerSession = Math.Round((double)model.TotalInputTokens / eventsWithTokens.Count, 0);
            model.AverageOutputTokensPerSession = Math.Round((double)model.TotalOutputTokens / eventsWithTokens.Count, 0);
        }

        var closedSessions = events.Where(evt => evt.SessionEndedUtc.HasValue).ToList();

        if (closedSessions.Count > 0)
        {
            model.HasResolutionData = true;
            model.AIResolvedSessions = closedSessions.Count(evt => evt.IsResolved);
            model.AIUnresolvedSessions = closedSessions.Count(evt => !evt.IsResolved);
            model.AIResolutionRatePercent = Math.Round((double)model.AIResolvedSessions / closedSessions.Count * 100, 1);
        }

        var sessionsWithConversion = events
            .Where(evt => evt.ConversionScore.HasValue && evt.ConversionMaxScore.HasValue && evt.ConversionMaxScore.Value > 0)
            .ToList();

        if (sessionsWithConversion.Count > 0)
        {
            model.HasConversionData = true;
            model.SessionsWithConversionData = sessionsWithConversion.Count;
            model.TotalConversionScore = sessionsWithConversion.Sum(evt => evt.ConversionScore!.Value);
            model.TotalConversionMaxScore = sessionsWithConversion.Sum(evt => evt.ConversionMaxScore!.Value);
            model.AverageConversionScorePercent = Math.Round((double)model.TotalConversionScore / model.TotalConversionMaxScore * 100, 1);
            model.HighPerformingSessions = sessionsWithConversion.Count(evt => (double)evt.ConversionScore!.Value / evt.ConversionMaxScore!.Value >= 0.7);
            model.LowPerformingSessions = sessionsWithConversion.Count(evt => (double)evt.ConversionScore!.Value / evt.ConversionMaxScore!.Value < 0.3);
            model.HighPerformingPercent = Math.Round((double)model.HighPerformingSessions / sessionsWithConversion.Count * 100, 1);
            model.LowPerformingPercent = Math.Round((double)model.LowPerformingSessions / sessionsWithConversion.Count * 100, 1);
        }

        model.ThumbsUpCount = events.Sum(evt => evt.ThumbsUpCount);
        model.ThumbsDownCount = events.Sum(evt => evt.ThumbsDownCount);
        model.NoFeedbackCount = events.Count(evt => evt.ThumbsUpCount == 0 && evt.ThumbsDownCount == 0);
        model.TotalRatings = model.ThumbsUpCount + model.ThumbsDownCount;
        model.HasFeedbackData = model.TotalRatings > 0;

        if (model.TotalRatings > 0)
        {
            model.ThumbsUpPercent = Math.Round((double)model.ThumbsUpCount / model.TotalRatings * 100, 1);
            model.ThumbsDownPercent = Math.Round((double)model.ThumbsDownCount / model.TotalRatings * 100, 1);
        }

        if (events.Count > 0)
        {
            var sessionsWithFeedback = events.Count(evt => evt.ThumbsUpCount > 0 || evt.ThumbsDownCount > 0);
            model.FeedbackRatePercent = Math.Round((double)sessionsWithFeedback / events.Count * 100, 1);
        }

        model.SessionsByHour = Enumerable.Range(0, 24)
            .ToDictionary(
                hour => $"{hour:00}:00",
                hour => events.Count(evt => evt.SessionStartedUtc.Hour == hour));

        model.SessionsByDayOfWeek = Enum.GetValues<DayOfWeek>()
            .ToDictionary(
                day => day.ToString(),
                day => events.Count(evt => evt.SessionStartedUtc.DayOfWeek == day));

        model.SessionsByUserSegment = new Dictionary<string, int>
        {
            ["Authenticated"] = events.Count(evt => evt.IsAuthenticated),
            ["Anonymous"] = events.Count(evt => !evt.IsAuthenticated),
        };
    }

    private static string GenerateCsv(IReadOnlyList<AIChatSessionEvent> events)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SessionId,ProfileId,VisitorId,UserId,IsAuthenticated,SessionStartedUtc,SessionEndedUtc,MessageCount,HandleTimeSeconds,IsResolved,TotalInputTokens,TotalOutputTokens,AverageResponseLatencyMs,ThumbsUpCount,ThumbsDownCount,ConversionScore,ConversionMaxScore");

        foreach (var evt in events)
        {
            builder.Append(EscapeCsv(evt.SessionId)).Append(',')
                .Append(EscapeCsv(evt.ProfileId)).Append(',')
                .Append(EscapeCsv(evt.VisitorId)).Append(',')
                .Append(EscapeCsv(evt.UserId)).Append(',')
                .Append(evt.IsAuthenticated).Append(',')
                .Append(evt.SessionStartedUtc.ToString("o")).Append(',')
                .Append(evt.SessionEndedUtc?.ToString("o") ?? string.Empty).Append(',')
                .Append(evt.MessageCount).Append(',')
                .Append(evt.HandleTimeSeconds).Append(',')
                .Append(evt.IsResolved).Append(',')
                .Append(evt.TotalInputTokens).Append(',')
                .Append(evt.TotalOutputTokens).Append(',')
                .Append(evt.AverageResponseLatencyMs).Append(',')
                .Append(evt.ThumbsUpCount).Append(',')
                .Append(evt.ThumbsDownCount).Append(',')
                .Append(evt.ConversionScore?.ToString() ?? string.Empty).Append(',')
                .AppendLine(evt.ConversionMaxScore?.ToString() ?? string.Empty);
        }

        return builder.ToString();
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
