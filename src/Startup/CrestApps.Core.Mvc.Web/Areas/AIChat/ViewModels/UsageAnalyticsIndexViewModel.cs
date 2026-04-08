namespace CrestApps.Core.Mvc.Web.Areas.AIChat.ViewModels;

public sealed class UsageAnalyticsIndexViewModel
{
    public bool IsAIUsageTrackingEnabled { get; set; }

    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }

    public bool ShowReport { get; set; }

    public int TotalCalls { get; set; }

    public int TotalSessions { get; set; }

    public int TotalChatInteractions { get; set; }

    public long TotalTokens { get; set; }

    public IReadOnlyList<AICompletionUsageSummaryViewModel> Rows { get; set; } = [];
}
