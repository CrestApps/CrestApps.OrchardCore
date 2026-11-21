using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// A quiz course item on a running site: the learner starts it from the course player, answers it on the Surveys
/// runner, and the attempt (a survey response with the enrollment as its context) gives the course item its result.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class QuizOnSurveysTests
{
    private readonly LearningSiteFixture _fixture;

    public QuizOnSurveysTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ALearner_TakesAQuizOnTheSurveysRunner_AndTheCourseItemRecordsTheGrade()
    {
        // Arrange
        var setup = await CreateCourseAsync();
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{setup.CourseId}/play/{setup.QuizId}", TestContext.Current.CancellationToken);

        // Act
        await LearningSurveyTestSupport.AnswerOverHttpAsync(learner, $"learning/quizzes/{setup.QuizId}/start", player, new Dictionary<string, string>
        {
            ["q.planet"] = "mars",
        });

        var (progress, responses) = await _fixture.InTenantAsync(async services =>
        {
            var item = (await services.GetRequiredService<ILearnerProgressService>().GetAsync(setup.EnrollmentId))?.Find(setup.QuizId);
            var list = await services.GetRequiredService<ILearningSurveyService>().ListEnrollmentResponsesAsync(setup.SurveyId, setup.EnrollmentId);

            return (item, list);
        });

        // Assert
        var response = Assert.Single(responses);
        Assert.Equal(SurveyResponseStatus.Completed, response.Status);
        Assert.Equal(GradingStatus.Graded, response.Grading);
        Assert.Equal(100m, response.Percent);
        Assert.Equal(LearningSurveyContexts.Enrollment(setup.EnrollmentId), response.Context);
        Assert.Equal(CourseItemState.Completed, progress?.State);
        Assert.Equal(100m, progress?.Score);
        Assert.True(progress?.Passed);
    }

    private Task<(string CourseId, string QuizId, string SurveyId, string EnrollmentId)> CreateCourseAsync()
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var suffix = Guid.NewGuid().ToString("n")[..6];
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

            course.DisplayText = "Quiz on Surveys " + suffix;
            course.Alter<TitlePart>(part => part.Title = course.DisplayText);
            course.Alter<CoursePart>(part => part.EnrollmentMode = EnrollmentMode.Open);
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

            var surveyId = LearningSurveyTestSupport.SurveyIdOf(quiz);
            var definition = LearningSurveyTestSupport.Definition(
                quiz.DisplayText,
                LearningSurveyTestSupport.SingleChoice("planet", "Which planet is red?", ("venus", "Venus"), ("mars", "Mars")));

            definition.Pages[0].Questions[0].Scoring = new SurveyQuestionScoring { Points = 1, CorrectChoiceIds = ["mars"] };
            definition.Scoring.Enabled = true;
            definition.Scoring.PassMarkPercent = 50;
            await LearningSurveyTestSupport.PublishAsync(services, surveyId, definition);

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
                SourceId = "quiz-on-surveys-" + suffix,
            });

            Assert.True(enrolled.Succeeded, enrolled.ErrorCode);

            return (course.ContentItemId, quiz.ContentItemId, surveyId, enrolled.Enrollment.ItemId);
        });
}
