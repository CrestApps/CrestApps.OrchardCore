using System.Net;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// A learner takes a quiz with the features a Moodle quiz needs on a running site: two questions per page, a
/// description, an ungraded survey, drag into text, gapselect and immediate feedback, then sees what the review options
/// allow right after the attempt.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class QuizMoodleFeaturesTests
{
    private readonly LearningSiteFixture _fixture;

    public QuizMoodleFeaturesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ALearner_TakesAPagedQuiz_WithTheNewQuestionTypesAndImmediateFeedback()
    {
        // Arrange
        var quizId = await CreateQuizAsync();
        using var learner = await _fixture.CreateClientAsync("learner1");
        var play = await learner.GetStringAsync($"learning/courses/{_fixture.CourseId}/play/{quizId}", TestContext.Current.CancellationToken);
        var start = await learner.PostAsync($"learning/quizzes/{quizId}/start", Form(("__RequestVerificationToken", LearningSiteFixture.Token(play))), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var attemptUrl = start.Headers.Location!.OriginalString.TrimStart('/');
        var attemptId = attemptUrl[(attemptUrl.LastIndexOf('/') + 1)..];
        var attempt = await AttemptAsync(attemptId);

        // Act: page one holds the description and the survey, and nothing after them.
        var first = await learner.GetStringAsync(attemptUrl, TestContext.Current.CancellationToken);
        var token = LearningSiteFixture.Token(first);

        // Check the survey: its answer is locked and its feedback shown.
        var check = await learner.PostAsync($"learning/attempts/{attemptId}/check/1", Form(("__RequestVerificationToken", token), ("a1", "agree")), TestContext.Current.CancellationToken);
        var checkedPage = await learner.GetStringAsync(attemptUrl, TestContext.Current.CancellationToken);

        // Move to page two and answer the drag into text and gapselect questions.
        var next = await learner.PostAsync($"learning/attempts/{attemptId}/submit", Form(("__RequestVerificationToken", token), ("next", "1")), TestContext.Current.CancellationToken);
        var second = await learner.GetStringAsync(attemptUrl + "?page=1", TestContext.Current.CancellationToken);
        string Choice(int index, string text) => attempt.Items[index].Choices.Single(choice => choice.Text == text).Id;
        var submit = await learner.PostAsync($"learning/attempts/{attemptId}/submit", Form(
            ("__RequestVerificationToken", LearningSiteFixture.Token(second)),
            ("a2_b0", Choice(2, "heat")),
            ("a2_b1", Choice(2, "water")),
            ("a3_b0", Choice(3, "Scotland"))), TestContext.Current.CancellationToken);
        var result = await learner.GetStringAsync(attemptUrl, TestContext.Current.CancellationToken);
        var done = await AttemptAsync(attemptId);

        // Assert
        Assert.Contains("Before you start", first, StringComparison.Ordinal);
        Assert.Contains("How was the training?", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Fire needs", first, StringComparison.Ordinal);
        Assert.Contains("data-learning-check", first, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, check.StatusCode);
        Assert.Contains("Thanks for your view.", checkedPage, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, next.StatusCode);
        Assert.Contains("Fire needs", second, StringComparison.Ordinal);
        Assert.Contains("data-blank-choices", second, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, submit.StatusCode);
        Assert.Equal(AttemptStatus.Graded, done.Status);
        Assert.NotNull(done.Items[1].CheckedUtc);
        Assert.Equal(4m, done.MaxScore);
        Assert.Equal(3m, done.Score);
        Assert.Contains("75%", result, StringComparison.Ordinal);
        Assert.Contains("Right answer", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AssessmentsConstants.ContentTypes.SurveyQuestion, "SurveyQuestionPart.Options")]
    [InlineData(AssessmentsConstants.ContentTypes.DragIntoTextQuestion, "ChoiceBlanksQuestionPart.Distractors")]
    [InlineData(AssessmentsConstants.ContentTypes.GapSelectQuestion, "ChoiceBlanksQuestionPart.Distractors")]
    [InlineData(AssessmentsConstants.ContentTypes.DescriptionQuestion, "QuestionPart.Prompt")]
    [InlineData(AssessmentsConstants.ContentTypes.Quiz, "QuizPart.QuestionsPerPage")]
    public async Task TheEditors_OfTheNewQuestionTypesAndQuizSettings_RenderInCards(string contentType, string field)
    {
        // Arrange
        using var admin = await _fixture.CreateClientAsync("admin");

        // Act
        var page = await admin.GetAsync($"Admin/Contents/ContentTypes/{contentType}/Create", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains($"name=\"{field}", html, StringComparison.Ordinal);
        Assert.Contains("card-header", html, StringComparison.Ordinal);
    }

    private Task<AssessmentAttempt> AttemptAsync(string attemptId)
        => _fixture.InTenantAsync(async services => await services.GetRequiredService<AssessmentAttemptStore>().FindByIdAsync(attemptId));

    private Task<string> CreateQuizAsync()
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var suffix = " " + Guid.NewGuid().ToString("N")[..6];

            async Task<ContentItem> QuestionAsync(string type, string title, decimal points, Action<ContentItem> configure)
            {
                var question = await contentManager.NewAsync(type);
                question.DisplayText = title + suffix;
                question.Alter<TitlePart>(part => part.Title = title + suffix);
                question.Alter<QuestionPart>(part => part.Points = points);
                configure(question);
                await contentManager.CreateAsync(question, VersionOptions.Published);

                return question;
            }

            var description = await QuestionAsync(AssessmentsConstants.ContentTypes.DescriptionQuestion, "Intro", 0, question => question.Alter<QuestionPart>(part => part.Prompt = "<p>Before you start, read the rules.</p>"));
            var survey = await QuestionAsync(AssessmentsConstants.ContentTypes.SurveyQuestion, "Opinion", 0, question =>
            {
                question.Alter<QuestionPart>(part =>
                {
                    part.Prompt = "<p>How was the training?</p>";
                    part.GeneralFeedback = "<p>Thanks for your view.</p>";
                });
                question.Alter<SurveyQuestionPart>(part => part.Options = [new ChoiceOption { Id = "agree", Text = "Good" }, new ChoiceOption { Id = "disagree", Text = "Poor" }]);
            });
            var drag = await QuestionAsync(AssessmentsConstants.ContentTypes.DragIntoTextQuestion, "Fire triangle", 2, question =>
            {
                question.Alter<QuestionPart>(part => part.Prompt = "<p>Drag the words.</p>");
                question.Alter<ChoiceBlanksQuestionPart>(part =>
                {
                    part.Text = "Fire needs [[heat]] and [[fuel]].";
                    part.Distractors = ["water"];
                });
            });
            var gaps = await QuestionAsync(AssessmentsConstants.ContentTypes.GapSelectQuestion, "Capitals", 2, question =>
            {
                question.Alter<QuestionPart>(part => part.Prompt = "<p>Choose the country.</p>");
                question.Alter<ChoiceBlanksQuestionPart>(part =>
                {
                    part.Text = "Edinburgh is in [[Scotland]].";
                    part.Distractors = ["England"];
                });
            });

            var course = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.Published);
            var quiz = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.Quiz);
            quiz.DisplayText = "Moodle quiz" + suffix;
            quiz.Alter<TitlePart>(part => part.Title = "Moodle quiz" + suffix);
            quiz.Alter<QuizPart>(part =>
            {
                part.QuestionIds = [description.ContentItemId, survey.ContentItemId, drag.ContentItemId, gaps.ContentItemId];
                part.MaxAttempts = null;
                part.QuestionsPerPage = 2;
                part.FeedbackMode = QuizFeedbackMode.Immediate;
                part.ReviewOptions = new QuizReviewOptions
                {
                    During = ReviewShows.GeneralFeedback,
                    ImmediatelyAfter = ReviewShows.All,
                    LaterWhileOpen = ReviewShows.Score,
                    AfterClose = ReviewShows.Score,
                };
            });
            quiz.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            quiz.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.Submitted);
            await contentManager.CreateAsync(quiz, VersionOptions.Published);

            var draft = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.DraftRequired);
            draft.Alter<CourseOutlinePart>(part => part.Sections[0].Items.Add(new CourseOutlineItem { ContentItemId = quiz.ContentItemId, Required = false }));
            await contentManager.UpdateAsync(draft);
            await contentManager.PublishAsync(draft);

            return quiz.ContentItemId;
        });

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
        => new(fields.Select(field => new KeyValuePair<string, string>(field.Key, field.Value)));
}
