namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for day-of-week usage distribution.
/// </summary>
public class ChatAnalyticsDayOfWeekViewModel
{
    public List<DayOfWeekUsage> DayDistribution { get; set; } = [];
}

public class DayOfWeekUsage
{
    public int DayIndex { get; set; }

    public string Label { get; set; }

    public int SessionCount { get; set; }
}
