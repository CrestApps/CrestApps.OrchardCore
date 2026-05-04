namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for conversion metrics analytics, including AI resolution detection
/// and goal-based conversion scoring.
/// </summary>
public class ChatAnalyticsConversionViewModel
{
    /// <summary>
    /// Gets or sets the total sessions.
    /// </summary>
    public int TotalSessions { get; set; }

    /// <summary>
    /// Gets or sets the sessions with conversion data.
    /// </summary>
    public int SessionsWithConversionData { get; set; }

    /// <summary>
    /// Gets or sets the average conversion score percent.
    /// </summary>
    public double AverageConversionScorePercent { get; set; }

    /// <summary>
    /// Gets or sets the total conversion score.
    /// </summary>
    public int TotalConversionScore { get; set; }

    /// <summary>
    /// Gets or sets the total conversion max score.
    /// </summary>
    public int TotalConversionMaxScore { get; set; }

    /// <summary>
    /// Gets or sets the AI resolved sessions.
    /// </summary>
    public int AIResolvedSessions { get; set; }

    /// <summary>
    /// Gets or sets the AI unresolved sessions.
    /// </summary>
    public int AIUnresolvedSessions { get; set; }

    /// <summary>
    /// Gets or sets the AI resolution rate percent.
    /// </summary>
    public double AIResolutionRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the high performing sessions.
    /// </summary>
    public int HighPerformingSessions { get; set; }

    /// <summary>
    /// Gets or sets the low performing sessions.
    /// </summary>
    public int LowPerformingSessions { get; set; }

    /// <summary>
    /// Gets or sets the high performing percent.
    /// </summary>
    public double HighPerformingPercent { get; set; }

    /// <summary>
    /// Gets or sets the low performing percent.
    /// </summary>
    public double LowPerformingPercent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has conversion data.
    /// </summary>
    public bool HasConversionData { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has resolution data.
    /// </summary>
    public bool HasResolutionData { get; set; }
}
