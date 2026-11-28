using System.Net;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Changing a given score of a course quiz on the Surveys grading page needs Override grades, as grading graded coursework
/// again does: a course instructor who grades the quiz without it sees the graded attempt without the change score tools,
/// and their posts are refused.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class QuizScoreChangeTests
{
    private readonly LearningSiteFixture _fixture;

    public QuizScoreChangeTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AnInstructorWithoutOverrideGrades_CantChangeTheScoreOfAGradedQuizAttempt()
    {
        // Arrange: a quiz of a course instructor1 teaches, answered and auto-graded.
        var responseId = await CreateGradedAttemptAsync();

        using var instructor = await _fixture.CreateClientAsync("instructor1");
        using var admin = await _fixture.CreateClientAsync("admin");

        // Act
        var instructorPage = await instructor.GetAsync($"Admin/surveys/grading/{responseId}", TestContext.Current.CancellationToken);
        var instructorHtml = await instructorPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var changed = await instructor.PostAsync($"Admin/surveys/grading/{responseId}/score", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(instructorHtml),
            ["NewScore"] = "0",
            ["Reason"] = "Changed without Override grades",
        }), TestContext.Current.CancellationToken);
        var adminHtml = await admin.GetStringAsync($"Admin/surveys/grading/{responseId}", TestContext.Current.CancellationToken);

        var score = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ISurveyResponseQueries>().FindByIdAsync(responseId)).Score);

        // Assert
        Assert.Equal(HttpStatusCode.OK, instructorPage.StatusCode);
        Assert.DoesNotContain("Change Score", instructorHtml);
        Assert.DoesNotContain("Save Grades", instructorHtml);
        // Refused: the site answers a forbidden post with its access denied page, not the grading page a change returns to.
        Assert.NotEqual(HttpStatusCode.OK, changed.StatusCode);
        Assert.DoesNotContain($"surveys/grading/{responseId}", changed.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Change Score", adminHtml);
        Assert.Equal(1m, score);
    }

    private Task<string> CreateGradedAttemptAsync()
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var suffix = Guid.NewGuid().ToString("n")[..6];
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

            course.DisplayText = "Quiz score changes " + suffix;
            course.Alter<TitlePart>(part => part.Title = course.DisplayText);
            course.Alter<CoursePart>(part =>
            {
                part.EnrollmentMode = EnrollmentMode.Open;
                part.Staff = [new CourseStaffMember { UserId = _fixture.InstructorId, Role = LearningConstants.CourseStaffRoles.Instructor }];
            });
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            var quiz = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.Quiz);

            quiz.DisplayText = "Planets quiz " + suffix;
            quiz.Alter<TitlePart>(part => part.Title = quiz.DisplayText);
            quiz.Alter<QuizPart>(part => part.GradingMethod = GradingMethod.Highest);
            quiz.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.Submitted);
            quiz.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            await contentManager.CreateAsync(quiz, VersionOptions.Published);

            var definition = LearningSurveyTestSupport.Definition(
                quiz.DisplayText,
                LearningSurveyTestSupport.SingleChoice("planet", "Which planet is red?", ("venus", "Venus"), ("mars", "Mars")));

            definition.Pages[0].Questions[0].Scoring = new SurveyQuestionScoring { Points = 1, CorrectChoiceIds = ["mars"] };
            definition.Scoring.Enabled = true;
            await LearningSurveyTestSupport.PublishAsync(services, LearningSurveyTestSupport.SurveyIdOf(quiz), definition);

            course.Alter<CourseOutlinePart>(part => part.Sections =
            [
                new CourseSection { Id = "s1", Title = "Quiz", Items = [new CourseOutlineItem { ContentItemId = quiz.ContentItemId }] },
            ]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            var enrolled = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = _fixture.EnrolledUserId,
                CourseId = course.ContentItemId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "quiz-score-change-" + suffix,
            });

            Assert.True(enrolled.Succeeded, enrolled.ErrorCode);

            var response = await LearningSurveyTestSupport.AnswerAsync(services, "learner1", quiz, new Dictionary<string, string> { ["q.planet"] = "mars" });

            Assert.Equal(GradingStatus.Graded, response.Grading);

            return response.ItemId;
        });
}
