namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// When Learning's hourly upkeep raises its notices. Bound from <c>CrestApps:Learning:Upkeep</c>.
/// </summary>
public sealed class LearningUpkeepOptions
{
    /// <summary>
    /// Gets or sets how many days before access ends the access-ending notice is raised. Zero turns it off.
    /// </summary>
    public int AccessEndingNoticeDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets after how many days without activity a learner counts as inactive. Zero turns it off.
    /// </summary>
    public int InactiveDays { get; set; } = 14;
}
