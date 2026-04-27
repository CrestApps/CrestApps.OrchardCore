namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model containing the computed analytics overview KPIs.
/// </summary>
public class ChatAnalyticsOverviewViewModel
{
    /// <summary>
    /// Gets or sets the total sessions.
    /// </summary>
    public int TotalSessions { get; set; }

    /// <summary>
    /// Gets or sets the unique visitors.
    /// </summary>
    public int UniqueVisitors { get; set; }

    /// <summary>
    /// Gets or sets the total visits.
    /// </summary>
    public int TotalVisits { get; set; }

    /// <summary>
    /// Gets or sets the average handle time seconds.
    /// </summary>
    public double AverageHandleTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the containment rate percent.
    /// </summary>
    public double ContainmentRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the abandonment rate percent.
    /// </summary>
    public double AbandonmentRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the resolved sessions.
    /// </summary>
    public int ResolvedSessions { get; set; }

    /// <summary>
    /// Gets or sets the abandoned sessions.
    /// </summary>
    public int AbandonedSessions { get; set; }

    /// <summary>
    /// Gets or sets the average messages per session.
    /// </summary>
    public double AverageMessagesPerSession { get; set; }

    /// <summary>
    /// Gets or sets the returning user rate percent.
    /// </summary>
    public double ReturningUserRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the active sessions.
    /// </summary>
    public int ActiveSessions { get; set; }

    /// <summary>
    /// Gets or sets the average steps to resolution.
    /// </summary>
    public double AverageStepsToResolution { get; set; }
}
