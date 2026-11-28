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
/// When the last requirement is met, the player says so: the step that completes the course leads to a completion
/// panel in the player, with the date, the transcript link and the way back, and the course page shows the same panel.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CourseCompletedPanelTests
{
    private readonly LearningSiteFixture _fixture;

    public CourseCompletedPanelTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CompletingTheLastItem_LeadsToTheCompletionPanel_InThePlayerAndOnTheCoursePage()
    {
        // Arrange
        var (courseId, itemId, enrollmentId, coursePath) = await CreateCourseAsync();
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{courseId}/play/{itemId}", TestContext.Current.CancellationToken);

        // Act
        var done = await learner.PostAsync($"learning/progress/{enrollmentId}/{itemId}/done", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(player),
        }), TestContext.Current.CancellationToken);
        var panel = await learner.GetStringAsync(done.Headers.Location!.OriginalString.TrimStart('/'), TestContext.Current.CancellationToken);
        var coursePage = await learner.GetStringAsync(coursePath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        Assert.EndsWith($"learning/courses/{courseId}/completed", done.Headers.Location!.OriginalString);
        Assert.Contains("data-learning-course-completed-panel", panel);
        Assert.Contains("You Completed This Course", panel);
        Assert.Contains("/learning/transcript", panel);
        Assert.Contains("data-learning-course-completed-panel", coursePage);
    }

    [Fact]
    public async Task TheCompletionPage_OfACourseNotCompleted_LeadsBackToThePlayer()
    {
        // Arrange
        var (courseId, _, _, _) = await CreateCourseAsync();
        using var learner = await _fixture.CreateClientAsync("learner1");

        // Act
        var response = await learner.GetAsync($"learning/courses/{courseId}/completed", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith($"learning/courses/{courseId}/play", response.Headers.Location!.OriginalString);
    }

    private Task<(string CourseId, string ItemId, string EnrollmentId, string CoursePath)> CreateCourseAsync()
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var suffix = Guid.NewGuid().ToString("n")[..6];
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

            course.DisplayText = "Completion " + suffix;
            course.Alter<TitlePart>(part => part.Title = course.DisplayText);
            course.Alter<CoursePart>(part => part.EnrollmentMode = EnrollmentMode.Open);
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            var page = await contentManager.NewAsync(LearningConstants.ContentTypes.PageLesson);

            page.DisplayText = "Only lesson " + suffix;
            page.Alter<TitlePart>(part => part.Title = page.DisplayText);
            page.Alter<HtmlBodyPart>(part => part.Html = "<p>Read it.</p>");
            page.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.MarkedDone);
            page.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            await contentManager.CreateAsync(page, VersionOptions.Published);

            course.Alter<CourseOutlinePart>(part => part.Sections =
            [
                new CourseSection { Id = "s1", Title = "Only", Items = [new CourseOutlineItem { ContentItemId = page.ContentItemId }] },
            ]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            var enrolled = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = _fixture.EnrolledUserId,
                CourseId = course.ContentItemId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "completion-" + suffix,
            });

            Assert.True(enrolled.Succeeded, enrolled.ErrorCode);

            var path = $"Contents/ContentItems/{course.ContentItemId}";

            return (course.ContentItemId, page.ContentItemId, enrolled.Enrollment.ItemId, path);
        });
}
