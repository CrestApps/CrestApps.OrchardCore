namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for day-of-week usage distribution.
/// </summary>
public class ChatAnalyticsDayOfWeekViewModel
{
    /// <summary>
    /// Gets or sets the day distribution.
    /// </summary>
    public List<DayOfWeekUsage> DayDistribution { get; set; } = [];
}

/// <summary>
/// Represents the day of week usage.
/// </summary>
public class DayOfWeekUsage
{
    /// <summary>
    /// Gets or sets the day index.
    /// </summary>
    public int DayIndex { get; set; }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the session count.
    /// </summary>
    public int SessionCount { get; set; }
}
