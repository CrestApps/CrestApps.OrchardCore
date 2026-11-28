using System.Net;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The admin pages of coursework render their rows (a row view that takes the row instead of
/// <c>ShapeViewModel&lt;T&gt;</c> fails only on a running site): the submissions of a coursework item, the grading page
/// of a submission and the merged grading list. A learner is refused them.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningCourseworkPagesTests
{
    private readonly LearningSiteFixture _fixture;

    public LearningCourseworkPagesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheSubmissionsGradingAndGradingListPages_RenderTheirRows_AndAreRefusedToALearner()
    {
        // Arrange: a coursework item and a hand-in by the enrolled learner that waits for a grader.
        var title = "Incident report " + Guid.NewGuid().ToString("n")[..6];
        var answer = "The forklift reversed without a spotter " + Guid.NewGuid().ToString("n")[..6];
        var (courseworkId, submissionId) = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var item = await contentManager.NewAsync(CourseworkConstants.ContentTypes.Coursework);

            item.DisplayText = title;
            item.Apply(new CourseworkPart { MaxPoints = 20 });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            var submission = new Submission
            {
                ItemId = IdGenerator.GenerateId(),
                EnrollmentId = _fixture.EnrollmentId,
                UserId = _fixture.EnrolledUserId,
                CourseId = _fixture.CourseId,
                CourseworkId = item.ContentItemId,
                CourseworkTitle = title,
                Text = answer,
                SubmittedUtc = DateTime.UtcNow,
                Status = SubmissionStatus.Submitted,
                Files = [new SubmissionFile { FileId = "f1", FileName = "photo.pdf", Size = 2048 }],
            };

            await services.GetRequiredService<SubmissionStore>().CreateAsync(submission);

            return (item.ContentItemId, submission.ItemId);
        });

        // Act
        using var admin = await _fixture.CreateClientAsync("admin");
        var list = await admin.GetAsync($"Admin/learning/coursework/{courseworkId}/submissions", TestContext.Current.CancellationToken);
        var listPage = await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var grading = await admin.GetAsync($"Admin/learning/coursework/submissions/{submissionId}", TestContext.Current.CancellationToken);
        var gradingPage = await grading.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var queue = await admin.GetAsync("Admin/learning/grading", TestContext.Current.CancellationToken);
        var queuePage = await queue.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var learner = await _fixture.CreateClientAsync("learner1");
        var refusedList = await learner.GetAsync($"Admin/learning/coursework/{courseworkId}/submissions", TestContext.Current.CancellationToken);
        var refusedGrading = await learner.GetAsync($"Admin/learning/coursework/submissions/{submissionId}", TestContext.Current.CancellationToken);
        var refusedQueue = await learner.GetAsync("Admin/learning/grading", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("learner1", listPage);
        Assert.Contains("Waits for a grader", listPage);
        Assert.Equal(HttpStatusCode.OK, grading.StatusCode);
        Assert.Contains(answer, gradingPage);
        Assert.Contains("photo.pdf", gradingPage);
        Assert.Contains("action-bar-bottom", gradingPage);
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        Assert.Contains(title, queuePage);
        Assert.Contains($"coursework/submissions/{submissionId}", queuePage);
        Assert.NotEqual(HttpStatusCode.OK, refusedList.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, refusedGrading.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, refusedQueue.StatusCode);
    }

    [Fact]
    public async Task AGraderWithoutOverrideGrades_IsNotOfferedGradeAgain_OnGradedWork()
    {
        // Arrange: browser pass 2026-11-18. Grading graded work needs Override grades, and the form ended in a 403 page.
        var title = "Graded report " + Guid.NewGuid().ToString("n")[..6];
        var submissionId = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var item = await contentManager.NewAsync(CourseworkConstants.ContentTypes.Coursework);

            item.DisplayText = title;
            item.Apply(new CourseworkPart { MaxPoints = 20 });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            var submission = new Submission
            {
                ItemId = IdGenerator.GenerateId(),
                EnrollmentId = _fixture.EnrollmentId,
                UserId = _fixture.EnrolledUserId,
                CourseId = _fixture.CourseId,
                CourseworkId = item.ContentItemId,
                CourseworkTitle = title,
                Text = "Graded already",
                SubmittedUtc = DateTime.UtcNow.AddHours(-2),
                Status = SubmissionStatus.Graded,
                Score = 60,
                Points = 12,
                MaxPoints = 20,
                GraderId = "admin",
                GradedUtc = DateTime.UtcNow.AddHours(-1),
            };

            await services.GetRequiredService<SubmissionStore>().CreateAsync(submission);

            return submission.ItemId;
        });

        // Act: instructor1 grades the course (Manage own courses) but does not hold Override grades.
        using var instructor = await _fixture.CreateClientAsync("instructor1");
        var page = await instructor.GetAsync($"Admin/learning/coursework/submissions/{submissionId}", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Graded already", html);
        Assert.DoesNotContain("Grade Again", html);
    }

    [Fact]
    public async Task TheRubricAndCourseworkEditors_Open()
    {
        // Arrange: the rubric editor once failed on a partial view it could not find.
        using var admin = await _fixture.CreateClientAsync("admin");

        // Act
        var rubric = await admin.GetAsync($"Admin/Contents/ContentTypes/{CourseworkConstants.ContentTypes.Rubric}/Create", TestContext.Current.CancellationToken);
        var rubricPage = await rubric.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var coursework = await admin.GetAsync($"Admin/Contents/ContentTypes/{CourseworkConstants.ContentTypes.Coursework}/Create", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, rubric.StatusCode);
        Assert.Contains("RubricPart.Criteria", rubricPage, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, coursework.StatusCode);
    }
}
