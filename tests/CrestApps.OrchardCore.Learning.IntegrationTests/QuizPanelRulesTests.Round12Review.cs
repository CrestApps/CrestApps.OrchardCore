using System.Net;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Round 12 review: a course survey with an access statement asks the learner to accept it and starts once they do; and
/// a quiz attempt past its deadline asks again for what a new attempt needs, since pressing the button starts one.
/// </summary>
public sealed partial class QuizPanelRulesTests
{
    private const string FeedbackStatement = "My answers are my own view of the course.";

    [Fact]
    public async Task R12Review_ACourseSurveyWithAStatement_AsksForIt_AndStartsOnlyOnceAccepted()
    {
        // Arrange
        var setup = await CreateCourseSurveyWithStatementAsync();
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync(setup.PlayerPath, TestContext.Current.CancellationToken);

        // Act
        var refused = await PostCourseSurveyStartAsync(learner, setup.ItemId, player, acceptStatement: false);
        var started = await PostCourseSurveyStartAsync(learner, setup.ItemId, player, acceptStatement: true);
        var responses = await _fixture.InTenantAsync(services =>
            services.GetRequiredService<ILearningSurveyService>().ListEnrollmentResponsesAsync(setup.SurveyId, setup.EnrollmentId));

        // Assert
        Assert.Contains(FeedbackStatement, player, StringComparison.Ordinal);
        Assert.Contains("name=\"acceptStatement\"", player, StringComparison.Ordinal);
        Assert.DoesNotContain("/s/r/", refused.Headers.Location?.OriginalString ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
        Assert.Contains("/s/r/", started.Headers.Location?.OriginalString, StringComparison.Ordinal);
        var response = Assert.Single(responses);
        Assert.NotNull(response.StatementAcceptedUtc);
    }

    [Fact]
    public async Task R12Review_AQuizAttemptPastItsDeadline_AsksForTheAccessCodeOfTheNextAttempt()
    {
        // Arrange: an attempt started with the access code, whose time ran out while the learner was away.
        var setup = await CreateQuizCourseAsync(definition => { }, survey => { });

        await _fixture.InTenantAsync(async services =>
        {
            var survey = await services.GetRequiredService<ISurveyManager>().FindByIdAsync(setup.SurveyId);

            await services.GetRequiredService<QuizStartSettings>().SaveAsync(survey, QuizAccessCode, removeAccessCode: false, statement: null);
        });

        using var learner = await _fixture.CreateClientAsync("learner1");
        var path = $"learning/courses/{setup.CourseId}/play/{setup.QuizId}";
        var player = await learner.GetStringAsync(path, TestContext.Current.CancellationToken);
        var started = await PostQuizStartAsync(learner, setup.QuizId, player, QuizAccessCode, acceptStatement: false);

        Assert.Contains("/s/r/", started.Headers.Location?.OriginalString, StringComparison.Ordinal);

        await _fixture.InTenantAsync(async services =>
        {
            var responses = services.GetRequiredService<ICatalog<SurveyResponse>>();
            var attempt = (await services.GetRequiredService<ILearningSurveyService>().ListEnrollmentResponsesAsync(setup.SurveyId, setup.EnrollmentId)).Single();
            var stored = await responses.FindByIdAsync(attempt.ItemId, TestContext.Current.CancellationToken);

            stored.DeadlineUtc = DateTime.UtcNow.AddMinutes(-5);
            await responses.UpdateAsync(stored, TestContext.Current.CancellationToken);
        });

        // Act
        var expired = await learner.GetStringAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("name=\"accessCode\"", expired, StringComparison.Ordinal);
        Assert.Contains("Attempt Again", expired, StringComparison.Ordinal);
        Assert.DoesNotContain("Continue Your Attempt", expired, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> PostCourseSurveyStartAsync(HttpClient client, string itemId, string player, bool acceptStatement)
    {
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(player),
        };

        if (acceptStatement)
        {
            form["acceptStatement"] = "true";
        }

        return client.PostAsync($"learning/surveys/{itemId}/start", new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
    }

    private Task<(string PlayerPath, string ItemId, string SurveyId, string EnrollmentId)> CreateCourseSurveyWithStatementAsync()
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await NewCourseAsync(contentManager, "Survey statement");
            var item = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.FeedbackSurvey, "Course feedback", CompletionModes.Submitted);

            item.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            var surveyId = LearningSurveyTestSupport.SurveyIdOf(item);

            await LearningSurveyTestSupport.PublishAsync(services, surveyId, Feedback(), survey =>
            {
                survey.Access ??= new SurveyAccessSettings();
                survey.Access.Statement = FeedbackStatement;
            });

            // A second item keeps the course from completing, so the learner stays on an active enrollment.
            var lesson = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.Choice, "Next step", CompletionModes.Submitted);

            lesson.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(lesson, VersionOptions.Published);

            var enrollmentId = await PublishAndEnrollAsync(services, contentManager, course, [item.ContentItemId, lesson.ContentItemId]);

            return ($"learning/courses/{course.ContentItemId}/play/{item.ContentItemId}", item.ContentItemId, surveyId, enrollmentId);
        });
}
