namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The video player of a video lesson.
/// </summary>
public class VideoPlayerViewModel
{
    public string VideoUrl { get; set; }

    public string VideoType { get; set; }

    public string CaptionsUrl { get; set; }

    public string EmbedUrl { get; set; }

    public string Transcript { get; set; }

    public int RequiredPercent { get; set; }
}
