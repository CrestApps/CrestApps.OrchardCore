using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that computes and renders conversion and AI resolution metrics
/// for the chat analytics dashboard.
/// </summary>
public sealed class AIChatAnalyticsConversionDisplayDriver : DisplayDriver<AIChatAnalyticsReport>
{
    public override IDisplayResult Display(AIChatAnalyticsReport context, BuildDisplayContext buildContext)
    {
        return Initialize<ChatAnalyticsConversionViewModel>("ChatAnalyticsConversion", model =>
        {
            var events = context.Events;

            model.TotalSessions = events.Count;

            // AI Resolution Detection metrics.
            var closedSessions = events.Where(e => e.SessionEndedUtc.HasValue).ToList();

            if (closedSessions.Count > 0)
            {
                model.HasResolutionData = true;
                model.AIResolvedSessions = closedSessions.Count(e => e.IsResolved);
                model.AIUnresolvedSessions = closedSessions.Count(e => !e.IsResolved);
                model.AIResolutionRatePercent = Math.Round((double)model.AIResolvedSessions / closedSessions.Count * 100, 1);
            }

            // Conversion metrics (goal-based scoring).
            var sessionsWithConversion = events
                .Where(e => e.ConversionScore.HasValue && e.ConversionMaxScore.HasValue && e.ConversionMaxScore.Value > 0)
                .ToList();

            if (sessionsWithConversion.Count > 0)
            {
                model.HasConversionData = true;
                model.SessionsWithConversionData = sessionsWithConversion.Count;
                model.TotalConversionScore = sessionsWithConversion.Sum(e => e.ConversionScore!.Value);
                model.TotalConversionMaxScore = sessionsWithConversion.Sum(e => e.ConversionMaxScore!.Value);

                model.AverageConversionScorePercent = Math.Round(
                    (double)model.TotalConversionScore / model.TotalConversionMaxScore * 100, 1);

                // High performing: sessions scoring >= 70% of max.
                model.HighPerformingSessions = sessionsWithConversion
                    .Count(e => (double)e.ConversionScore!.Value / e.ConversionMaxScore!.Value >= 0.7);
                model.LowPerformingSessions = sessionsWithConversion
                    .Count(e => (double)e.ConversionScore!.Value / e.ConversionMaxScore!.Value < 0.3);

                model.HighPerformingPercent = Math.Round(
                    (double)model.HighPerformingSessions / sessionsWithConversion.Count * 100, 1);
                model.LowPerformingPercent = Math.Round(
                    (double)model.LowPerformingSessions / sessionsWithConversion.Count * 100, 1);
            }
        }).Location("Content:5");
    }
}
