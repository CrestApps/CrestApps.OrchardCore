using System.Net;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Round 12: a course survey takes one response per learner, and "Change your answers" reopens it (L1); an anonymous
/// quiz submitted without the learner's request completes on their next visit (M5); and a quiz's access code and honor
/// statement are asked for on the quiz panel and enforced by the start (L18).
/// </summary>
public sealed partial class QuizPanelRulesTests
{
    private const string QuizAccessCode = "Quiz-code-42";
    private const string HonorStatement = "I will take this quiz on my own.";

    [Fact]
    public async Task R12L1_ACourseSurvey_TakesOneCompletedResponsePerLearner()
    {
        // Arrange
        var setup = await CreateCourseSurveyAsync(AnonymityMode.Identified, allowChange: false);
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync(setup.PlayerPath, TestContext.Current.CancellationToken);

        await LearningSurveyTestSupport.AnswerOverHttpAsync(learner, $"learning/surveys/{setup.ItemId}/start", player, new Dictionary<string, string>
        {
            ["q.again"] = "yes",
        });

        // Act: a second start, as a forged post of the start form would make.
        var second = await StartAsLearnerAsync(setup.ItemId, setup.SurveyId);
        var responses = await _fixture.InTenantAsync(services =>
            services.GetRequiredService<ILearningSurveyService>().ListEnrollmentResponsesAsync(setup.SurveyId, setup.EnrollmentId));

        // Assert
        Assert.False(second.Succeeded);
        Assert.Equal(SurveyErrorCode.LimitReached, second.Error);
        Assert.Single(responses);
    }

    [Fact]
    public async Task R12L1_AnAnonymousCourseSurvey_IsNotOfferedAgainAfterAnswering()
    {
        // Arrange
        var setup = await CreateCourseSurveyAsync(AnonymityMode.Anonymous, allowChange: false);
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync(setup.PlayerPath, TestContext.Current.CancellationToken);

        await LearningSurveyTestSupport.AnswerOverHttpAsync(learner, $"learning/surveys/{setup.ItemId}/start", player, new Dictionary<string, string>
        {
            ["q.again"] = "yes",
        });

        // Act
        var after = await learner.GetStringAsync(setup.PlayerPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Answer the Survey", player, StringComparison.Ordinal);
        Assert.DoesNotContain("Answer the Survey", after, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R12L1_ChangeYourAnswers_ReopensTheSameResponse()
    {
        // Arrange
        var setup = await CreateCourseSurveyAsync(AnonymityMode.Identified, allowChange: true);
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync(setup.PlayerPath, TestContext.Current.CancellationToken);

        await LearningSurveyTestSupport.AnswerOverHttpAsync(learner, $"learning/surveys/{setup.ItemId}/start", player, new Dictionary<string, string>
        {
            ["q.again"] = "yes",
        });

        var answered = await learner.GetStringAsync(setup.PlayerPath, TestContext.Current.CancellationToken);

        // Act
        var change = await learner.PostAsync($"learning/surveys/{setup.ItemId}/start", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(answered),
        }), TestContext.Current.CancellationToken);
        var responses = await _fixture.InTenantAsync(services =>
            services.GetRequiredService<ILearningSurveyService>().ListEnrollmentResponsesAsync(setup.SurveyId, setup.EnrollmentId));

        // Assert
        Assert.Contains("Change Your Answers", answered, StringComparison.Ordinal);
        var response = Assert.Single(responses);
        Assert.Equal(SurveyResponseStatus.InProgress, response.Status);
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);
        Assert.EndsWith("/s/r/" + response.ItemId, change.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R12M5_AnAnonymousQuizCompletedWithoutTheLearner_IsRecordedOnTheirNextVisit()
    {
        // Arrange: an anonymous quiz the learner completed, whose completion never reached their progress (as when the
        // deadline task submits it, with no learner to ask). A second item keeps the course from completing.
        var setup = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await NewCourseAsync(contentManager, "Anonymous quiz");
            var quiz = await NewItemAsync(contentManager, course, AssessmentsConstants.ContentTypes.Quiz, "Planets quiz", CompletionModes.Submitted);

            quiz.Alter<QuizPart>(_ => { });
            await contentManager.CreateAsync(quiz, VersionOptions.Published);

            var definition = LearningSurveyTestSupport.Definition(
                quiz.DisplayText,
                LearningSurveyTestSupport.SingleChoice("planet", "Which planet is red?", ("venus", "Venus"), ("mars", "Mars")));

            definition.Pages[0].Questions[0].Scoring = new SurveyQuestionScoring { Points = 1, CorrectChoiceIds = ["mars"] };
            definition.Scoring.Enabled = true;
            await LearningSurveyTestSupport.PublishAsync(services, LearningSurveyTestSupport.SurveyIdOf(quiz), definition, survey =>
            {
                survey.Access ??= new SurveyAccessSettings();
                survey.Access.Anonymity = AnonymityMode.Anonymous;
            });

            var lesson = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.Choice, "Next step", CompletionModes.Submitted);

            lesson.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(lesson, VersionOptions.Published);

            var enrollmentId = await PublishAndEnrollAsync(services, contentManager, course, [quiz.ContentItemId, lesson.ContentItemId]);

            return (CourseId: course.ContentItemId, QuizId: quiz.ContentItemId, EnrollmentId: enrollmentId);
        });
        using var learner = await _fixture.CreateClientAsync("learner1");
        var path = $"learning/courses/{setup.CourseId}/play/{setup.QuizId}";
        var player = await learner.GetStringAsync(path, TestContext.Current.CancellationToken);

        await LearningSurveyTestSupport.AnswerOverHttpAsync(learner, $"learning/quizzes/{setup.QuizId}/start", player, new Dictionary<string, string>
        {
            ["q.planet"] = "mars",
        });

        var reopened = await _fixture.InTenantAsync(async services =>
        {
            var enrollment = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(setup.EnrollmentId);
            await services.GetRequiredService<ILearnerProgressService>().ReopenItemAsync(enrollment, setup.QuizId);

            return (await services.GetRequiredService<ILearnerProgressService>().GetAsync(setup.EnrollmentId))?.Find(setup.QuizId)?.State;
        });

        // Act: the learner's next visit.
        await learner.GetStringAsync(path, TestContext.Current.CancellationToken);

        var progress = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ILearnerProgressService>().GetAsync(setup.EnrollmentId))?.Find(setup.QuizId));

        // Assert
        Assert.NotEqual(CourseItemState.Completed, reopened);
        Assert.Equal(CourseItemState.Completed, progress?.State);
    }

    [Fact]
    public async Task R12L18_AQuizWithAnAccessCodeAndAStatement_AsksForBoth_AndStartsOnlyWithBoth()
    {
        // Arrange
        var setup = await CreateQuizCourseAsync(definition => { }, survey => { });

        await _fixture.InTenantAsync(async services =>
        {
            var survey = await services.GetRequiredService<ISurveyManager>().FindByIdAsync(setup.SurveyId);

            await services.GetRequiredService<QuizStartSettings>().SaveAsync(survey, QuizAccessCode, removeAccessCode: false, HonorStatement);
        });

        using var learner = await _fixture.CreateClientAsync("learner1");
        var path = $"learning/courses/{setup.CourseId}/play/{setup.QuizId}";
        var player = await learner.GetStringAsync(path, TestContext.Current.CancellationToken);

        // Act
        var wrongCode = await PostQuizStartAsync(learner, setup.QuizId, player, QuizAccessCode + "x", acceptStatement: true);
        var noStatement = await PostQuizStartAsync(learner, setup.QuizId, player, QuizAccessCode, acceptStatement: false);
        var started = await PostQuizStartAsync(learner, setup.QuizId, player, QuizAccessCode, acceptStatement: true);
        var responses = await _fixture.InTenantAsync(services =>
            services.GetRequiredService<ILearningSurveyService>().ListEnrollmentResponsesAsync(setup.SurveyId, setup.EnrollmentId));

        // Assert
        Assert.Contains("name=\"accessCode\"", player, StringComparison.Ordinal);
        Assert.Contains(HonorStatement, player, StringComparison.Ordinal);
        Assert.Contains("name=\"acceptStatement\"", player, StringComparison.Ordinal);
        Assert.DoesNotContain("/s/r/", wrongCode.Headers.Location?.OriginalString ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("/s/r/", noStatement.Headers.Location?.OriginalString ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
        Assert.Contains("/s/r/", started.Headers.Location?.OriginalString, StringComparison.Ordinal);
        var response = Assert.Single(responses);
        Assert.NotNull(response.StatementAcceptedUtc);
    }

    private static Task<HttpResponseMessage> PostQuizStartAsync(HttpClient client, string quizId, string player, string accessCode, bool acceptStatement)
    {
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(player),
            ["accessCode"] = accessCode,
        };

        if (acceptStatement)
        {
            form["acceptStatement"] = "true";
        }

        return client.PostAsync($"learning/quizzes/{quizId}/start", new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
    }

    private Task<SurveyStartResult> StartAsLearnerAsync(string itemId, string surveyId)
        => _fixture.InTenantAsync(async services =>
        {
            var user = await services.GetRequiredService<UserManager<IUser>>().FindByNameAsync("learner1");
            var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);
            var item = await services.GetRequiredService<IContentManager>().GetAsync(itemId, VersionOptions.Published);

            services.GetRequiredService<IHttpContextAccessor>().HttpContext.User = principal;

            return await services.GetRequiredService<ILearningSurveyService>().StartAsync(principal, item, surveyId, null);
        });

    private Task<(string PlayerPath, string ItemId, string SurveyId, string EnrollmentId)> CreateCourseSurveyAsync(AnonymityMode anonymity, bool allowChange)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await NewCourseAsync(contentManager, "One response");
            var item = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.FeedbackSurvey, "Course feedback", CompletionModes.Submitted);

            item.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            var surveyId = LearningSurveyTestSupport.SurveyIdOf(item);
            var definition = Feedback();

            definition.Navigation.AllowEditAfterSubmit = allowChange;
            await LearningSurveyTestSupport.PublishAsync(services, surveyId, definition, survey =>
            {
                survey.Access ??= new SurveyAccessSettings();
                survey.Access.Anonymity = anonymity;
            });

            // A second item keeps the course from completing, so the learner stays on an active enrollment.
            var lesson = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.Choice, "Next step", CompletionModes.Submitted);

            lesson.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(lesson, VersionOptions.Published);

            var enrollmentId = await PublishAndEnrollAsync(services, contentManager, course, [item.ContentItemId, lesson.ContentItemId]);

            return ($"learning/courses/{course.ContentItemId}/play/{item.ContentItemId}", item.ContentItemId, surveyId, enrollmentId);
        });
}
