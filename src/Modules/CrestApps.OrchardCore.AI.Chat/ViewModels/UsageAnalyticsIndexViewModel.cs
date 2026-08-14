namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for usage analytics index.
/// </summary>
public sealed class UsageAnalyticsIndexViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether is AI usage tracking enabled.
    /// </summary>
    public bool IsAIUsageTrackingEnabled { get; set; }

    /// <summary>
    /// Gets or sets the start date in local time.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date in local time.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the show report.
    /// </summary>
    public bool ShowReport { get; set; }

    /// <summary>
    /// Gets or sets the total calls.
    /// </summary>
    public int TotalCalls { get; set; }

    /// <summary>
    /// Gets or sets the total sessions.
    /// </summary>
    public int TotalSessions { get; set; }

    /// <summary>
    /// Gets or sets the total chat interactions.
    /// </summary>
    public int TotalChatInteractions { get; set; }

    /// <summary>
    /// Gets or sets the total tokens.
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public IReadOnlyList<AICompletionUsageSummaryViewModel> Rows { get; set; } = [];
}
