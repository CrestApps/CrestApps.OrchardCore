using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that computes and renders model/system performance metrics
/// including response latency and token usage.
/// </summary>
public sealed class AIChatAnalyticsPerformanceDisplayDriver : DisplayDriver<AIChatAnalyticsReport>
{
    public override IDisplayResult Display(AIChatAnalyticsReport context, BuildDisplayContext buildContext)
    {
        return Initialize<ChatAnalyticsPerformanceViewModel>("ChatAnalyticsPerformance", model =>
        {
            var events = context.Events;

            var eventsWithLatency = events.Where(e => e.AverageResponseLatencyMs > 0).ToList();
            var eventsWithTokens = events.Where(e => e.TotalInputTokens > 0 || e.TotalOutputTokens > 0).ToList();

            model.SessionsWithLatencyData = eventsWithLatency.Count;
            model.SessionsWithTokenData = eventsWithTokens.Count;
            model.HasData = eventsWithLatency.Count > 0 || eventsWithTokens.Count > 0;

            if (eventsWithLatency.Count > 0)
            {
                model.AverageResponseLatencyMs = Math.Round(eventsWithLatency.Average(e => e.AverageResponseLatencyMs), 0);
            }

            if (eventsWithTokens.Count > 0)
            {
                model.TotalInputTokens = eventsWithTokens.Sum(e => (long)e.TotalInputTokens);
                model.TotalOutputTokens = eventsWithTokens.Sum(e => (long)e.TotalOutputTokens);
                model.TotalTokens = model.TotalInputTokens + model.TotalOutputTokens;
                model.AverageTokensPerSession = Math.Round((double)model.TotalTokens / eventsWithTokens.Count, 0);
                model.AverageInputTokensPerSession = Math.Round((double)model.TotalInputTokens / eventsWithTokens.Count, 0);
                model.AverageOutputTokensPerSession = Math.Round((double)model.TotalOutputTokens / eventsWithTokens.Count, 0);
            }
        }).Location("Content:5");
    }
}
