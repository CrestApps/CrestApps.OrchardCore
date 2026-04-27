namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for edit AI profile analytics.
/// </summary>
public class EditAIProfileAnalyticsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether enable session metrics.
    /// </summary>
    public bool EnableSessionMetrics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether enable AI resolution detection.
    /// </summary>
    public bool EnableAIResolutionDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether enable conversion metrics.
    /// </summary>
    public bool EnableConversionMetrics { get; set; }

    /// <summary>
    /// Gets or sets the conversion goals.
    /// </summary>
    public List<ConversionGoalViewModel> ConversionGoals { get; set; } = [];
}

/// <summary>
/// Represents the view model for conversion goal.
/// </summary>
public class ConversionGoalViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the min score.
    /// </summary>
    public int MinScore { get; set; }

    /// <summary>
    /// Gets or sets the max score.
    /// </summary>
    public int MaxScore { get; set; } = 10;
}
