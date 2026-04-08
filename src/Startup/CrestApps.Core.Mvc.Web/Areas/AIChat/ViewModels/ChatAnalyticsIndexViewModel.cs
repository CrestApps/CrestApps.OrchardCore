using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.ViewModels;

public sealed class ChatAnalyticsIndexViewModel
{
    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }

    public string ProfileId { get; set; }

    public IReadOnlyList<SelectListItem> Profiles { get; set; } = [];

    public bool ShowReport { get; set; }

    public int TotalSessions { get; set; }

    public int UniqueVisitors { get; set; }

    public int ResolvedSessions { get; set; }

    public int AbandonedSessions { get; set; }

    public int ActiveSessions { get; set; }

    public double ContainmentRatePercent { get; set; }

    public double AbandonmentRatePercent { get; set; }

    public double AverageHandleTimeSeconds { get; set; }

    public double AverageMessagesPerSession { get; set; }

    public double ReturningUserRatePercent { get; set; }

    public double AverageStepsToResolution { get; set; }

    public bool HasPerformanceData { get; set; }

    public int SessionsWithLatencyData { get; set; }

    public int SessionsWithTokenData { get; set; }

    public double AverageResponseLatencyMs { get; set; }

    public long TotalInputTokens { get; set; }

    public long TotalOutputTokens { get; set; }

    public long TotalTokens { get; set; }

    public double AverageTokensPerSession { get; set; }

    public double AverageInputTokensPerSession { get; set; }

    public double AverageOutputTokensPerSession { get; set; }

    public bool HasResolutionData { get; set; }

    public int AIResolvedSessions { get; set; }

    public int AIUnresolvedSessions { get; set; }

    public double AIResolutionRatePercent { get; set; }

    public bool HasConversionData { get; set; }

    public int SessionsWithConversionData { get; set; }

    public int TotalConversionScore { get; set; }

    public int TotalConversionMaxScore { get; set; }

    public double AverageConversionScorePercent { get; set; }

    public int HighPerformingSessions { get; set; }

    public int LowPerformingSessions { get; set; }

    public double HighPerformingPercent { get; set; }

    public double LowPerformingPercent { get; set; }

    public bool HasFeedbackData { get; set; }

    public int ThumbsUpCount { get; set; }

    public int ThumbsDownCount { get; set; }

    public int NoFeedbackCount { get; set; }

    public int TotalRatings { get; set; }

    public double ThumbsUpPercent { get; set; }

    public double ThumbsDownPercent { get; set; }

    public double FeedbackRatePercent { get; set; }

    public IReadOnlyDictionary<string, int> SessionsByHour { get; set; } = new Dictionary<string, int>();

    public IReadOnlyDictionary<string, int> SessionsByDayOfWeek { get; set; } = new Dictionary<string, int>();

    public IReadOnlyDictionary<string, int> SessionsByUserSegment { get; set; } = new Dictionary<string, int>();
}
