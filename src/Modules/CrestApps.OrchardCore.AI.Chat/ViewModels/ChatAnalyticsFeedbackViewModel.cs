namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for user feedback (thumbs up/down) analytics.
/// Counts represent individual message-level ratings across all sessions.
/// </summary>
public class ChatAnalyticsFeedbackViewModel
{
    /// <summary>
    /// Gets or sets the thumbs up count.
    /// </summary>
    public int ThumbsUpCount { get; set; }

    /// <summary>
    /// Gets or sets the thumbs down count.
    /// </summary>
    public int ThumbsDownCount { get; set; }

    /// <summary>
    /// Gets or sets the no feedback count.
    /// </summary>
    public int NoFeedbackCount { get; set; }

    /// <summary>
    /// Gets or sets the thumbs up percent.
    /// </summary>
    public double ThumbsUpPercent { get; set; }

    /// <summary>
    /// Gets or sets the thumbs down percent.
    /// </summary>
    public double ThumbsDownPercent { get; set; }

    /// <summary>
    /// Gets or sets the feedback rate percent.
    /// </summary>
    public double FeedbackRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the total ratings.
    /// </summary>
    public int TotalRatings { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has data.
    /// </summary>
    public bool HasData { get; set; }
}
