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

            model.ThumbsUpCount = events.Count(e => e.UserRating == true);
            model.ThumbsDownCount = events.Count(e => e.UserRating == false);
            model.NoFeedbackCount = events.Count(e => e.UserRating == null);
            model.TotalSessionsWithFeedback = model.ThumbsUpCount + model.ThumbsDownCount;
            model.HasData = model.TotalSessionsWithFeedback > 0;

            if (model.TotalSessionsWithFeedback > 0)
            {
                model.ThumbsUpPercent = Math.Round((double)model.ThumbsUpCount / model.TotalSessionsWithFeedback * 100, 1);
                model.ThumbsDownPercent = Math.Round((double)model.ThumbsDownCount / model.TotalSessionsWithFeedback * 100, 1);
            }

            if (events.Count > 0)
            {
                model.FeedbackRatePercent = Math.Round((double)model.TotalSessionsWithFeedback / events.Count * 100, 1);
            }
        }).Location("Content:6");
    }
}
