namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The editors of the acknowledgement part and the video lesson part.
/// </summary>
public class LearningPartTextViewModel
{
    public string Statement { get; set; }

    public int RequiredPercent { get; set; } = 90;

    public string Transcript { get; set; }
}
