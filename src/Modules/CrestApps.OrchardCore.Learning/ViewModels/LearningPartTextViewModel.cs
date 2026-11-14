namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The editors of the acknowledgement part and the video lesson part.
/// </summary>
public class LearningPartTextViewModel
{
    public string Statement { get; set; }

    public int RequiredPercent { get; set; } = 90;

    public string Transcript { get; set; }

    /// <summary>
    /// Gets or sets the length of a video in minutes, when known.
    /// </summary>
    public decimal? DurationMinutes { get; set; }
}
