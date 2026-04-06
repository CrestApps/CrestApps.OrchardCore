using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Areas.AIChat.Services;
using CrestApps.Mvc.Web.Areas.AIChat.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CrestApps.Mvc.Web.Areas.AIChat.Controllers;

[Area("AIChat")]
[Authorize(Policy = "Admin")]
public sealed class UsageAnalyticsController : Controller
{
    private readonly MvcAICompletionUsageService _usageService;
    private readonly GeneralAIOptions _generalAIOptions;

    public UsageAnalyticsController(
        MvcAICompletionUsageService usageService,
        IOptions<GeneralAIOptions> generalAIOptions)
    {
        _usageService = usageService;
        _generalAIOptions = generalAIOptions.Value;
    }

    [HttpGet]
    public IActionResult Index() => View(new UsageAnalyticsIndexViewModel
    {
        IsAIUsageTrackingEnabled = _generalAIOptions.EnableAIUsageTracking,
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Index))]
    public async Task<IActionResult> IndexPost(UsageAnalyticsIndexViewModel model)
    {
        model.IsAIUsageTrackingEnabled = _generalAIOptions.EnableAIUsageTracking;
        var records = await _usageService.GetAsync(model.StartDateUtc, model.EndDateUtc);
        ApplyReport(model, records);

        return View("Index", model);
    }

    private static void ApplyReport(UsageAnalyticsIndexViewModel model, IReadOnlyList<AICompletionUsageRecord> records)
    {
        model.ShowReport = true;

        var relevantRecords = records
            .Where(record => !string.IsNullOrEmpty(record.SessionId) || !string.IsNullOrEmpty(record.InteractionId))
            .ToList();

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

        model.Rows = relevantRecords
            .GroupBy(record => new
            {
                UserLabel = GetUserLabel(record),
                record.IsAuthenticated,
                ClientName = record.ClientName ?? record.ProviderName ?? "Unknown",
                ModelName = record.ModelName ?? record.DeploymentName ?? "Unknown",
            })
            .Select(group =>
            {
                var latencySamples = group.Where(record => record.ResponseLatencyMs > 0).ToList();

                return new AICompletionUsageSummaryViewModel
                {
                    UserLabel = group.Key.UserLabel,
                    IsAuthenticated = group.Key.IsAuthenticated,
                    ClientName = group.Key.ClientName,
                    ModelName = group.Key.ModelName,
                    TotalCalls = group.Count(),
                    TotalSessions = group.Select(record => record.SessionId).Where(sessionId => !string.IsNullOrEmpty(sessionId)).Distinct(StringComparer.Ordinal).Count(),
                    TotalChatInteractions = group.Select(record => record.InteractionId).Where(interactionId => !string.IsNullOrEmpty(interactionId)).Distinct(StringComparer.Ordinal).Count(),
                    TotalInputTokens = group.Sum(record => (long)record.InputTokenCount),
                    TotalOutputTokens = group.Sum(record => (long)record.OutputTokenCount),
                    TotalTokens = group.Sum(record => (long)record.TotalTokenCount),
                    AverageResponseLatencyMs = latencySamples.Count > 0
                        ? Math.Round(latencySamples.Average(record => record.ResponseLatencyMs), 0)
                        : 0,
                };
            })
            .OrderByDescending(row => row.TotalTokens)
            .ThenByDescending(row => row.TotalCalls)
            .ThenBy(row => row.UserLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetUserLabel(AICompletionUsageRecord record)
    {
        if (!string.IsNullOrEmpty(record.UserName))
        {
            return record.UserName;
        }

        if (record.IsAuthenticated && !string.IsNullOrEmpty(record.UserId))
        {
            return record.UserId;
        }

        return "Anonymous";
    }
}
