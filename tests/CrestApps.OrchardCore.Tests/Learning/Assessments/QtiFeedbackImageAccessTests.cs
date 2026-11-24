using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// Review round 9: a picture of imported QTI feedback is read by a learner only once the review of an attempt of theirs
/// shows the feedback that holds it; staff are checked by permission (the QTI import integration test covers them).
/// </summary>
public sealed class QtiFeedbackImageAccessTests
{
    private const string FileId = "feedback-file-1";

    [Fact]
    public async Task ALearner_ReadsAFeedbackPicture_OnlyOnceTheirReviewShowsIt()
    {
        // Arrange: a question whose feedback shows the picture, in a quiz reviewed once graded.
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        var question = h.Questions.AddChoice("q1", "a");
        question.Alter<QuestionPart>(part => part.GeneralFeedback = $"<p><img src=\"/{QtiFeedbackImages.PathOf(FileId)}\" alt=\"Answer\" /></p>");
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["q1"];
            quiz.ReviewPolicy = ReviewPolicy.AfterGraded;
        });
        await h.EnrollAsync("learner", "course1");
        await h.EnrollAsync("other", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        await h.StartAsync("other", "quiz");

        // Act
        var whileAnswering = await CanReadAsync(h, "learner", FileId);
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }));
        await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));
        var afterGrading = await CanReadAsync(h, "learner", FileId);
        var anotherPicture = await CanReadAsync(h, "learner", "another-file");
        var anotherLearner = await CanReadAsync(h, "other", FileId);

        // Assert
        Assert.False(whileAnswering);
        Assert.True(afterGrading);
        Assert.False(anotherPicture);
        Assert.False(anotherLearner);
    }

    private static Task<bool> CanReadAsync(AssessmentTestHarness h, string userId, string fileId)
        => h.RunAsync(async services =>
        {
            // No permission of any kind: the person is a learner.
            var authorization = new Mock<IAuthorizationService>();
            authorization
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Failed());
            authorization
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()))
                .ReturnsAsync(AuthorizationResult.Failed());

            var scope = new GradingScope(authorization.Object, services.GetRequiredService<ICourseStructureService>());
            var access = new QtiFeedbackImageAccess(authorization.Object, scope, services.GetRequiredService<AssessmentAttemptStore>(), services.GetRequiredService<AttemptReviewPolicy>());
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));

            return await access.CanReadAsync(user, fileId);
        });
}
