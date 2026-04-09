namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model containing the computed analytics overview KPIs.
/// </summary>
public class ChatAnalyticsOverviewViewModel
{
    public int TotalSessions { get; set; }

    public int UniqueVisitors { get; set; }

    public int TotalVisits { get; set; }

    public double AverageHandleTimeSeconds { get; set; }

    public double ContainmentRatePercent { get; set; }

    public double AbandonmentRatePercent { get; set; }

    public int ResolvedSessions { get; set; }

    public int AbandonedSessions { get; set; }

    public double AverageMessagesPerSession { get; set; }

    public double ReturningUserRatePercent { get; set; }

    public int ActiveSessions { get; set; }

    public double AverageStepsToResolution { get; set; }
}
