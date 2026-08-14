namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for time-of-day usage distribution.
/// </summary>
public class ChatAnalyticsTimeOfDayViewModel
{
    /// <summary>
    /// Gets or sets the hourly distribution.
    /// </summary>
    public List<HourlyUsage> HourlyDistribution { get; set; } = [];
}

/// <summary>
/// Represents the hourly usage.
/// </summary>
public class HourlyUsage
{
    /// <summary>
    /// Gets or sets the hour.
    /// </summary>
    public int Hour { get; set; }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the session count.
    /// </summary>
    public int SessionCount { get; set; }
}
