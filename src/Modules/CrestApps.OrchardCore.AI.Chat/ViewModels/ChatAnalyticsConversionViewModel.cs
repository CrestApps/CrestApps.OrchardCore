namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for conversion metrics analytics, including AI resolution detection
/// and goal-based conversion scoring.
/// </summary>
public class ChatAnalyticsConversionViewModel
{
    public int TotalSessions { get; set; }

    public int SessionsWithConversionData { get; set; }

    public double AverageConversionScorePercent { get; set; }

    public int TotalConversionScore { get; set; }

    public int TotalConversionMaxScore { get; set; }

    public int AIResolvedSessions { get; set; }

    public int AIUnresolvedSessions { get; set; }

    public double AIResolutionRatePercent { get; set; }

    public int HighPerformingSessions { get; set; }

    public int LowPerformingSessions { get; set; }

    public double HighPerformingPercent { get; set; }

    public double LowPerformingPercent { get; set; }

    public bool HasConversionData { get; set; }

    public bool HasResolutionData { get; set; }
}
