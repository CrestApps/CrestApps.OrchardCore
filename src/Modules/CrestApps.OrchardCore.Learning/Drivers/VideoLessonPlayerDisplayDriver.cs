using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.ViewModels;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Media;
using OrchardCore.Media.Fields;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The player of a video lesson: the attached video file with its captions, or the video-site link, and the transcript.
/// Attached files are served by Orchard Core's media route, which Secure Media authorizes by the lesson.
/// </summary>
internal sealed class VideoLessonPlayerDisplayDriver : ContentPartDisplayDriver<VideoLessonPart>
{
    private readonly IMediaFileStore _mediaFileStore;

    public VideoLessonPlayerDisplayDriver(IMediaFileStore mediaFileStore)
    {
        _mediaFileStore = mediaFileStore;
    }

    public override IDisplayResult Display(VideoLessonPart part, BuildPartDisplayContext context)
    {
        return Initialize<VideoPlayerViewModel>("VideoLessonPart_Player", model =>
        {
            var item = part.ContentItem;
            var fields = item.Get<ContentPart>(item.ContentType);
            var video = fields?.Get<MediaField>("Video")?.Paths?.FirstOrDefault();
            var captions = fields?.Get<MediaField>("Captions")?.Paths?.FirstOrDefault();

            model.RequiredPercent = part.RequiredPercent;
            model.Transcript = part.Transcript;
            model.EmbedUrl = fields?.Get<YoutubeField>("YouTube")?.EmbeddedAddress;

            if (!string.IsNullOrEmpty(video))
            {
                model.VideoUrl = _mediaFileStore.MapPathToPublicUrl(video);
                model.VideoType = video.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ? "video/webm" : "video/mp4";
            }

            if (!string.IsNullOrEmpty(captions))
            {
                model.CaptionsUrl = _mediaFileStore.MapPathToPublicUrl(captions);
            }
        }).Location("Detail", "Content:5");
    }
}
