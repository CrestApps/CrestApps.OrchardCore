using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that computes and renders overview KPIs for chat analytics.
/// </summary>
public sealed class AIChatAnalyticsOverviewDisplayDriver : DisplayDriver<AIChatAnalyticsReport>
{
    public override IDisplayResult Display(AIChatAnalyticsReport context, BuildDisplayContext buildContext)
    {
        return Initialize<ChatAnalyticsOverviewViewModel>("ChatAnalyticsOverview", model =>
        {
            var events = context.Events;

            model.TotalSessions = events.Count;
            model.UniqueVisitors = events
                .Where(e => !string.IsNullOrEmpty(e.VisitorId))
                .Select(e => e.VisitorId)
                .Distinct()
                .Count();

            model.TotalVisits = events.Count;
            model.ResolvedSessions = events.Count(e => e.IsResolved);
            model.AbandonedSessions = events.Count(e => !e.IsResolved && e.SessionEndedUtc.HasValue);
            model.ActiveSessions = events.Count(e => !e.SessionEndedUtc.HasValue);

            if (model.TotalSessions > 0)
            {
                model.ContainmentRatePercent = Math.Round((double)model.ResolvedSessions / model.TotalSessions * 100, 1);
                model.AbandonmentRatePercent = Math.Round((double)model.AbandonedSessions / model.TotalSessions * 100, 1);

                var sessionsWithHandleTime = events.Where(e => e.HandleTimeSeconds > 0).ToList();
                model.AverageHandleTimeSeconds = sessionsWithHandleTime.Count > 0
                    ? Math.Round(sessionsWithHandleTime.Average(e => e.HandleTimeSeconds), 1)
                    : 0;

                model.AverageMessagesPerSession = Math.Round(events.Average(e => e.MessageCount), 1);

                // Returning user rate: visitors who engaged in more than one session.
                var visitorSessionCounts = events
                    .Where(e => !string.IsNullOrEmpty(e.VisitorId))
                    .GroupBy(e => e.VisitorId)
                    .ToList();

                if (visitorSessionCounts.Count > 0)
                {
                    var returningVisitors = visitorSessionCounts.Count(g => g.Count() > 1);
                    model.ReturningUserRatePercent = Math.Round((double)returningVisitors / visitorSessionCounts.Count * 100, 1);
                }

                // Average steps to resolution: avg messages for resolved sessions.
                var resolvedEvents = events.Where(e => e.IsResolved).ToList();
                if (resolvedEvents.Count > 0)
                {
                    model.AverageStepsToResolution = Math.Round(resolvedEvents.Average(e => e.MessageCount), 1);
                }
            }
        }).Location("Content:1");
    }
}
