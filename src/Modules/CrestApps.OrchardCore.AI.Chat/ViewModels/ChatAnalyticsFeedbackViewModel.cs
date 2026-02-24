namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for user feedback (thumbs up/down) analytics.
/// </summary>
public class ChatAnalyticsFeedbackViewModel
{
    public int ThumbsUpCount { get; set; }

    public int ThumbsDownCount { get; set; }

    public int NoFeedbackCount { get; set; }

    public double ThumbsUpPercent { get; set; }

    public double ThumbsDownPercent { get; set; }

    public double FeedbackRatePercent { get; set; }

    public int TotalSessionsWithFeedback { get; set; }

    public bool HasData { get; set; }
}
