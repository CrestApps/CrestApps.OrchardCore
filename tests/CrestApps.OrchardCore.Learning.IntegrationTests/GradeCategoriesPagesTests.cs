using System.Net;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Html.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Learning plan Phase 7 (section 7.6): with gradebook categories on a course, the course editor shows its categories,
/// the editor of a gradable item of the course offers them, and the gradebook shows a column per category.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class GradeCategoriesPagesTests
{
    private readonly LearningSiteFixture _fixture;

    public GradeCategoriesPagesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheCourseEditor_TheItemEditor_AndTheGradebook_ShowTheCategories()
    {
        // Arrange: a course with two categories, a gradable lesson in one of them and an enrolled learner.
        var suffix = Guid.NewGuid().ToString("n")[..6];
        var homework = "Homework " + suffix;
        var exams = "Exams " + suffix;
        var (courseId, lessonId) = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

            course.DisplayText = "Graded course " + suffix;
            course.Alter<TitlePart>(part => part.Title = course.DisplayText);
            course.Alter<CoursePart>(part =>
            {
                part.EnrollmentMode = EnrollmentMode.Open;
                part.GradeCategories =
                [
                    new GradeCategory { Id = "hw" + suffix, Name = homework, Weight = 40, DropLowest = 1 },
                    new GradeCategory { Id = "ex" + suffix, Name = exams, Weight = 60 },
                ];
            });
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            var lesson = await contentManager.NewAsync(LearningConstants.ContentTypes.PageLesson);

            lesson.DisplayText = "Worksheet " + suffix;
            lesson.Alter<TitlePart>(part => part.Title = lesson.DisplayText);
            lesson.Alter<HtmlBodyPart>(part => part.Html = "<p>Fill it in.</p>");
            lesson.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            lesson.Alter<CourseItemPart>(part =>
            {
                part.CompletionMode = CompletionModes.Viewed;
                part.Gradable = true;
                part.GradeCategoryId = "hw" + suffix;
            });
            await contentManager.CreateAsync(lesson, VersionOptions.Published);

            course.Alter<CourseOutlinePart>(part => part.Sections =
            [
                new CourseSection { Id = "s1", Title = "Work", Items = [new CourseOutlineItem { ContentItemId = lesson.ContentItemId }] },
            ]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            var enrolled = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = _fixture.SpareLearnerId,
                CourseId = course.ContentItemId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "grade-categories-test",
            });

            Assert.True(enrolled.Succeeded, enrolled.ErrorCode);

            return (course.ContentItemId, lesson.ContentItemId);
        });

        using var admin = await _fixture.CreateClientAsync("admin");

        // Act
        var courseEditor = await admin.GetAsync($"Admin/Contents/ContentItems/{courseId}/Edit", TestContext.Current.CancellationToken);
        var coursePage = await courseEditor.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var itemEditor = await admin.GetAsync($"Admin/Contents/ContentItems/{lessonId}/Edit", TestContext.Current.CancellationToken);
        var itemPage = await itemEditor.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var gradebook = await admin.GetAsync($"Admin/learning/gradebook?courseId={courseId}", TestContext.Current.CancellationToken);
        var gradebookPage = await gradebook.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, courseEditor.StatusCode);
        Assert.Contains(homework, coursePage, StringComparison.Ordinal);
        Assert.Contains("CoursePart.GradeCategories", coursePage, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, itemEditor.StatusCode);
        Assert.Contains("CourseItemPart.GradeCategoryId", itemPage, StringComparison.Ordinal);
        Assert.Contains(exams, itemPage, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, gradebook.StatusCode);
        Assert.Contains(homework, gradebookPage, StringComparison.Ordinal);
        Assert.Contains(exams, gradebookPage, StringComparison.Ordinal);
    }
}
