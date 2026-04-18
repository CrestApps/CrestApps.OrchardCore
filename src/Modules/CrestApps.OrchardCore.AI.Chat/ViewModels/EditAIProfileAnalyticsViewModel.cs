namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class EditAIProfileAnalyticsViewModel
{
    public bool EnableSessionMetrics { get; set; }

    public bool EnableAIResolutionDetection { get; set; } = true;

    public bool EnableConversionMetrics { get; set; }

    public List<ConversionGoalViewModel> ConversionGoals { get; set; } = [];
}

public class ConversionGoalViewModel
{
    public string Name { get; set; }

    public string Description { get; set; }

    public int MinScore { get; set; }

    public int MaxScore { get; set; } = 10;
}
