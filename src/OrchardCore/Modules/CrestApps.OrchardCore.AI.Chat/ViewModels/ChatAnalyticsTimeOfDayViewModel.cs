namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for time-of-day usage distribution.
/// </summary>
public class ChatAnalyticsTimeOfDayViewModel
{
    public List<HourlyUsage> HourlyDistribution { get; set; } = [];
}

public class HourlyUsage
{
    public int Hour { get; set; }

    public string Label { get; set; }

    public int SessionCount { get; set; }
}
