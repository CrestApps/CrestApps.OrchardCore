using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// The player settings of a video lesson. The video itself is a media field or a video-site field on the same type,
/// and captions are a WebVTT file in a media field.
/// </summary>
public sealed class VideoLessonPart : ContentPart
{
    /// <summary>
    /// Gets or sets the share of the video, from 1 to 100, the learner must watch for the <c>Watched</c> completion mode.
    /// </summary>
    public int RequiredPercent { get; set; } = 90;

    /// <summary>
    /// Gets or sets the transcript, shown under the player.
    /// </summary>
    public string Transcript { get; set; }

    /// <summary>
    /// Gets or sets the length of the video in seconds, when known. With it, a share watched is checked against the time
    /// since the learner opened the video; without it, the share is the browser's claim and is marked self-reported.
    /// </summary>
    public int? DurationSeconds { get; set; }
}
