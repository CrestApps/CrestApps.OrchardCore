using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that computes and renders user feedback metrics
/// (thumbs up/down ratio and feedback rate).
/// </summary>
public sealed class AIChatAnalyticsFeedbackDisplayDriver : DisplayDriver<AIChatAnalyticsReport>
{
    public override IDisplayResult Display(AIChatAnalyticsReport context, BuildDisplayContext buildContext)
    {
        return Initialize<ChatAnalyticsFeedbackViewModel>("ChatAnalyticsFeedback", model =>
        {
            var events = context.Events;
            model.ThumbsUpCount = events.Sum(e => e.ThumbsUpCount);
            model.ThumbsDownCount = events.Sum(e => e.ThumbsDownCount);
            model.NoFeedbackCount = events.Count(e => e.ThumbsUpCount == 0 && e.ThumbsDownCount == 0);
            model.TotalRatings = model.ThumbsUpCount + model.ThumbsDownCount;
            model.HasData = model.TotalRatings > 0;

            if (model.TotalRatings > 0)
            {
                model.ThumbsUpPercent = Math.Round((double)model.ThumbsUpCount / model.TotalRatings * 100, 1);
                model.ThumbsDownPercent = Math.Round((double)model.ThumbsDownCount / model.TotalRatings * 100, 1);
            }

            if (events.Count > 0)
            {
                var sessionsWithFeedback = events.Count(e => e.ThumbsUpCount > 0 || e.ThumbsDownCount > 0);
                model.FeedbackRatePercent = Math.Round((double)sessionsWithFeedback / events.Count * 100, 1);
            }
        }).Location("Content:6");
    }
}
