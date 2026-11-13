using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Html.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Media.Settings;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.Migrations;

/// <summary>
/// Creates the Learning index tables, the Learning parts and the Course and lesson content types. A type that already
/// exists is left as the site changed it; only the course list gains the course item types it does not offer.
/// </summary>
internal sealed class LearningMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly CourseListSynchronizer _courseListSynchronizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="courseListSynchronizer">The synchronizer of course lists.</param>
    public LearningMigrations(
        IContentDefinitionManager contentDefinitionManager,
        CourseListSynchronizer courseListSynchronizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _courseListSynchronizer = courseListSynchronizer;
    }

    /// <summary>
    /// The path of a course page: <c>courses/</c> and the slug of its title.
    /// </summary>
    public const string CoursePathPattern = "courses/{{ ContentItem.DisplayText | slugify }}";

    /// <summary>
    /// Creates the index tables, the parts and the content types.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> CreateAsync()
    {
        await LearningIndexSchema.CreateAsync(SchemaBuilder);
        await LearningIndexSchema.CreateCourseStaffIndexAsync(SchemaBuilder);

        await DefinePartsAsync();
        await DefineCourseAsync();
        await DefineLessonsAsync();
        await _courseListSynchronizer.SynchronizeAsync();

        return 2;
    }

    /// <summary>
    /// Fixes the course path pattern, which slugified the slash of <c>courses/</c> into a dash.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync(LearningConstants.ContentTypes.Course, type => type
            .WithPart(nameof(AutoroutePart), part => part.MergeSettings<AutoroutePartSettings>(settings => settings.Pattern = CoursePathPattern)));

        return 2;
    }

    private async Task DefinePartsAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(CoursePart), part => part
            .Attachable()
            .WithDisplayName("Course")
            .WithDescription("Makes a content type a course: its code, enrollment settings, completion criteria, prerequisites, pass mark and staff."));

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(CourseOutlinePart), part => part
            .Attachable()
            .WithDisplayName("Course outline")
            .WithDescription("Orders the items of a course into sections and says which ones count toward completion."));

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(CourseItemPart), part => part
            .Attachable()
            .WithDisplayName("Course item")
            .WithDescription("Makes a content type an item of a course: how it is completed, whether it is graded, whether it is a free preview and when it opens."));

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(VideoLessonPart), part => part
            .Attachable()
            .WithDisplayName("Video lesson")
            .WithDescription("The player settings of a video lesson: how much the learner must watch and the transcript."));

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(AcknowledgementPart), part => part
            .Attachable()
            .WithDisplayName("Acknowledgement")
            .WithDescription("A statement the learner confirms, such as \"I have read and understood the policy\"."));
    }

    private async Task DefineCourseAsync()
    {
        if (await _contentDefinitionManager.GetTypeDefinitionAsync(LearningConstants.ContentTypes.Course) is not null)
        {
            return;
        }

        await _contentDefinitionManager.AlterPartDefinitionAsync(LearningConstants.ContentTypes.Course, part => part
            .WithField("Cover", field => field
                .OfType("MediaField")
                .WithDisplayName("Cover image")
                .WithPosition("1")
                .WithSettings(new MediaFieldSettings
                {
                    Multiple = false,
                    Hint = "The picture shown in the catalog and at the top of the course page.",
                })));

        await _contentDefinitionManager.AlterTypeDefinitionAsync(LearningConstants.ContentTypes.Course, type => type
            .WithDisplayName("Course")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
            .WithPart(nameof(AutoroutePart), part => part
                .WithPosition("2")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = CoursePathPattern,
                }))
            .WithPart(nameof(HtmlBodyPart), part => part.WithPosition("3").WithEditor("Wysiwyg"))
            .WithPart(LearningConstants.ContentTypes.Course, part => part.WithPosition("4"))
            .WithPart(nameof(CoursePart), part => part.WithPosition("5"))
            .WithPart(nameof(CourseOutlinePart), part => part.WithPosition("6"))
            .WithPart(nameof(ListPart), part => part
                .WithPosition("7")
                .WithSettings(new ListPartSettings
                {
                    PageSize = 50,
                    ShowHeader = true,
                    ContainedContentTypes = [],
                })));
    }

    private async Task DefineLessonsAsync()
    {
        await DefineLessonAsync(LearningConstants.ContentTypes.PageLesson, "Page lesson", LearningConstants.CourseItemKinds.Page, CompletionModes.Viewed, async () =>
            await _contentDefinitionManager.AlterPartDefinitionAsync(LearningConstants.ContentTypes.PageLesson, part => part
                .WithField("Attachments", field => field
                    .OfType("MediaField")
                    .WithDisplayName("Attached files")
                    .WithEditor("Attached")
                    .WithPosition("1")
                    .WithSettings(new MediaFieldSettings
                    {
                        Multiple = true,
                        Hint = "Files the learner can open with the lesson. Only people who may open the lesson can open them.",
                    }))));

        await DefineLessonAsync(LearningConstants.ContentTypes.VideoLesson, "Video lesson", LearningConstants.CourseItemKinds.Video, CompletionModes.Watched, async () =>
            await _contentDefinitionManager.AlterPartDefinitionAsync(LearningConstants.ContentTypes.VideoLesson, part => part
                .WithField("Video", field => field
                    .OfType("MediaField")
                    .WithDisplayName("Video file")
                    .WithEditor("Attached")
                    .WithPosition("1")
                    .WithSettings(new MediaFieldSettings
                    {
                        Multiple = false,
                        Hint = "An MP4 or WebM file. Large files upload in parts. Leave empty to use a video-site link instead.",
                        AllowedExtensions = [".mp4", ".webm"],
                    }))
                .WithField("Captions", field => field
                    .OfType("MediaField")
                    .WithDisplayName("Captions")
                    .WithEditor("Attached")
                    .WithPosition("2")
                    .WithSettings(new MediaFieldSettings
                    {
                        Multiple = false,
                        Hint = "A WebVTT (.vtt) file with the captions of the video.",
                        AllowedExtensions = [".vtt"],
                    }))
                .WithField("YouTube", field => field
                    .OfType("YoutubeField")
                    .WithDisplayName("Video-site link")
                    .WithPosition("3"))),
            withVideoPart: true);

        await DefineLessonAsync(LearningConstants.ContentTypes.FileLesson, "File lesson", LearningConstants.CourseItemKinds.File, CompletionModes.Viewed, async () =>
            await _contentDefinitionManager.AlterPartDefinitionAsync(LearningConstants.ContentTypes.FileLesson, part => part
                .WithField("Files", field => field
                    .OfType("MediaField")
                    .WithDisplayName("Files")
                    .WithEditor("Attached")
                    .WithPosition("1")
                    .WithSettings(new MediaFieldSettings
                    {
                        Multiple = true,
                        Hint = "The files of the lesson. Only people who may open the lesson can open them.",
                    }))));

        await DefineLessonAsync(LearningConstants.ContentTypes.LinkLesson, "Link lesson", LearningConstants.CourseItemKinds.Link, CompletionModes.Viewed, async () =>
            await _contentDefinitionManager.AlterPartDefinitionAsync(LearningConstants.ContentTypes.LinkLesson, part => part
                .WithField("Url", field => field
                    .OfType("LinkField")
                    .WithDisplayName("Link")
                    .WithPosition("1")
                    .WithSettings(new LinkFieldSettings
                    {
                        Required = true,
                        Hint = "The page the learner opens.",
                        DefaultTarget = "_blank",
                    }))));

        await DefineLessonAsync(LearningConstants.ContentTypes.Acknowledgement, "Acknowledgement", LearningConstants.CourseItemKinds.Acknowledgement, CompletionModes.Acknowledged, () => Task.CompletedTask, withAcknowledgementPart: true);
    }

    private async Task DefineLessonAsync(string typeName, string displayName, string kind, string completionMode, Func<Task> defineFields, bool withVideoPart = false, bool withAcknowledgementPart = false)
    {
        if (await _contentDefinitionManager.GetTypeDefinitionAsync(typeName) is not null)
        {
            return;
        }

        await defineFields();

        await _contentDefinitionManager.AlterTypeDefinitionAsync(typeName, type =>
        {
            type
                .WithDisplayName(displayName)
                .Creatable()
                .Listable()
                .Draftable()
                .Versionable()
                .Securable()
                .WithPart(nameof(TitlePart), part => part.WithPosition("1"))
                .WithPart(nameof(HtmlBodyPart), part => part.WithPosition("2").WithEditor("Wysiwyg"))
                .WithPart(typeName, part => part.WithPosition("3"));

            if (withVideoPart)
            {
                type.WithPart(nameof(VideoLessonPart), part => part.WithPosition("4"));
            }

            if (withAcknowledgementPart)
            {
                type.WithPart(nameof(AcknowledgementPart), part => part.WithPosition("4"));
            }

            type.WithPart(nameof(CourseItemPart), part => part
                .WithPosition("10")
                .WithSettings(new CourseItemPartSettings
                {
                    Kind = kind,
                    DefaultCompletionMode = completionMode,
                }));
        });
    }
}
