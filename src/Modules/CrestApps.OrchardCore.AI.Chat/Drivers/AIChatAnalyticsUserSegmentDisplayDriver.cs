using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that computes and renders user segmentation metrics
/// (authenticated vs anonymous session breakdown).
/// </summary>
public sealed class AIChatAnalyticsUserSegmentDisplayDriver : DisplayDriver<AIChatAnalyticsReport>
{
    public override IDisplayResult Display(AIChatAnalyticsReport context, BuildDisplayContext buildContext)
    {
        return Initialize<ChatAnalyticsUserSegmentViewModel>("ChatAnalyticsUserSegment", model =>
        {
            var events = context.Events;

            model.AuthenticatedSessions = events.Count(e => e.IsAuthenticated);
            model.AnonymousSessions = events.Count(e => !e.IsAuthenticated);

            model.UniqueAuthenticatedUsers = events
                .Where(e => e.IsAuthenticated && !string.IsNullOrEmpty(e.UserId))
                .Select(e => e.UserId)
                .Distinct()
                .Count();

            model.UniqueAnonymousVisitors = events
                .Where(e => !e.IsAuthenticated && !string.IsNullOrEmpty(e.VisitorId))
                .Select(e => e.VisitorId)
                .Distinct()
                .Count();

            if (events.Count > 0)
            {
                model.AuthenticatedPercent = Math.Round((double)model.AuthenticatedSessions / events.Count * 100, 1);
                model.AnonymousPercent = Math.Round((double)model.AnonymousSessions / events.Count * 100, 1);
            }
        }).Location("Content:4");
    }
}
