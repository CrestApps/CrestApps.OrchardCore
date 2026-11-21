using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// The Text course item (a Moodle label): HTML shown in place in the outline and the player, never tracked for
/// completion, never counted in the progress, and never required.
/// </summary>
public sealed class TextItemTests
{
    [Fact]
    public void TheTextKind_IsRegistered_InlineWithoutCompletionModes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLearningCore();

        // Act
        var kind = services.BuildServiceProvider().GetRequiredService<IOptions<CourseItemKindOptions>>().Value.Find(LearningConstants.CourseItemKinds.Text);

        // Assert
        Assert.NotNull(kind);
        Assert.True(kind.Inline);
        Assert.Empty(kind.CompletionModes);
        Assert.False(kind.ProducesGrade);
        Assert.Equal(LearningConstants.ContentTypes.TextBlock, kind.ContentType);
    }

    [Fact]
    public void RequiredEntries_LeaveOutInlineEntries_AndThePercentIgnoresThem()
    {
        // Arrange
        var outline = new CourseOutline();
        var page = Entry("page", required: true, inline: false);
        var text = Entry("text", required: true, inline: true);
        outline.Entries.Add(text);
        outline.Entries.Add(page);
        var progress = new EnrollmentProgress();
        progress.Items.Add(new CourseItemProgress { ContentItemId = "page", State = CourseItemState.Completed });

        // Act
        var required = outline.RequiredEntries.ToArray();
        var percent = CourseCompletionEvaluator.ComputePercent(outline, progress);

        // Assert
        Assert.Equal([page], required);
        Assert.Equal(100, percent);
    }

    [Fact]
    public async Task TheOutline_MarksATextItemInline_AndNeverRequired()
    {
        // Arrange
        var (harness, _, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        var course = new ContentItem { ContentItemId = "course1", ContentItemVersionId = "course1-v1", ContentType = LearningConstants.ContentTypes.Course, Published = true, Latest = true };
        course.Alter<CoursePart>(_ => { });
        course.Alter<CourseOutlinePart>(part => part.Sections =
        [
            new CourseSection
            {
                Id = "s1",
                Items =
                [
                    new CourseOutlineItem { ContentItemId = "label", Required = true },
                    new CourseOutlineItem { ContentItemId = "page", Required = true },
                ],
            },
        ]);
        var label = Item("label", LearningConstants.CourseItemKinds.Text);
        var page = Item("page", LearningConstants.CourseItemKinds.Page);
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>())).ReturnsAsync([label, page]);

        // Act
        var outline = await harness.RunAsync(services =>
            ActivatorUtilities.CreateInstance<DefaultCourseStructureService>(services, contentManager.Object).GetOutlineAsync(course), commit: false);

        // Assert
        var text = outline.Find("label");
        Assert.True(text.Inline);
        Assert.False(text.Required);
        Assert.False(outline.Find("page").Inline);
        Assert.True(outline.Find("page").Required);
    }

    [Fact]
    public async Task AnInlineItem_TakesNoProgress_AndTheCourseCompletesWithoutIt()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "label", kind: LearningConstants.CourseItemKinds.Text, completionMode: null, inline: true);
        courses.AddItem("course1", "page");
        var enrolled = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = "user1",
            CourseId = "course1",
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-user1",
        }));

        // Act
        var text = await harness.RunAsync(async services =>
            await services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrolled.Enrollment.ItemId), "label", 30));
        var lesson = await harness.RunAsync(async services =>
            await services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrolled.Enrollment.ItemId), "page", 30));

        // Assert
        Assert.Equal(LearningRefusals.NotTracked, text.RefusedReason);
        Assert.True(lesson.CourseCompleted);
        Assert.Equal(100, lesson.Enrollment.ProgressPercent);
    }

    private static CourseOutlineEntry Entry(string id, bool required, bool inline)
        => new()
        {
            Item = new ContentItem { ContentItemId = id },
            Part = new CourseItemPart(),
            Required = required,
            Inline = inline,
        };

    private static ContentItem Item(string id, string kind)
    {
        var item = new ContentItem { ContentItemId = id, ContentItemVersionId = id + "-v1", ContentType = "Item", Published = true, Latest = true };
        item.Alter<CourseItemPart>(part => part.Kind = kind);
        item.Alter<ContainedPart>(part => part.ListContentItemId = "course1");

        return item;
    }
}
