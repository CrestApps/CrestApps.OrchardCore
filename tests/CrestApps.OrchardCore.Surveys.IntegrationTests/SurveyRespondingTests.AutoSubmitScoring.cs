using System.Net;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Over HTTP, the score of a branched quiz counts the respondent's whole path and nothing else: a submitted response
/// leaves the track a branch skipped out (P1-H2), and a response submitted for the respondent once the survey closed
/// counts the pages still ahead on their track as 0, so walking away doesn't pass the quiz (V-H1). The review page lists
/// the same questions the score counts.
/// </summary>
public sealed partial class SurveyRespondingTests
{
    [Fact]
    public async Task BranchedQuiz_Submitted_ScoresOnlyTheTrackTaken_AndTheReviewListsIt()
    {
        // Arrange: "a" goes on to track A (2 questions, 2 points), whose branch ends the quiz; "b" jumps to track B.
        var (surveyId, token, ids) = await PublishTrackQuizAsync("Track quiz submitted");
        using var http = await _fixture.CreateClientAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var (responsePath, page1) = await StartQuizAsync(http, token);
        var antiforgery = SurveysSiteFixture.Token(page1);

        await http.PostAsync(responsePath, Multipart(antiforgery, page1, "next", (ids["track"], ids["a"])), cancellationToken);
        var trackA = await http.GetStringAsync(responsePath, cancellationToken);

        // Act
        var finished = await http.PostAsync(responsePath, Multipart(antiforgery, trackA, "finish", (ids["qa1"], ids["qa1a"]), (ids["qa2"], ids["qa2a"])), cancellationToken);
        var completed = await LatestResponseAsync(surveyId);
        var review = await http.GetStringAsync($"{responsePath}/review", cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, finished.StatusCode);
        Assert.Equal(SurveyResponseStatus.Completed, completed.Status);
        Assert.Equal(4m, completed.Score);
        Assert.Equal(4m, completed.MaxScore);
        Assert.True(completed.Passed);
        Assert.Contains("Track A question one", review, StringComparison.Ordinal);
        Assert.DoesNotContain("Track B question one", review, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BranchedQuiz_LeftAfterTheFirstPage_SubmittedAtTheClose_CountsTheUnreachedTrackPagesAsZero()
    {
        // Arrange: the respondent picks track A and leaves without answering it.
        var (surveyId, token, ids) = await PublishTrackQuizAsync("Track quiz walked away");
        using var http = await _fixture.CreateClientAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var (responsePath, page1) = await StartQuizAsync(http, token);
        var antiforgery = SurveysSiteFixture.Token(page1);

        await http.PostAsync(responsePath, Multipart(antiforgery, page1, "next", (ids["track"], ids["a"])), cancellationToken);
        var trackA = await http.GetStringAsync(responsePath, cancellationToken);

        // Act: the closing date passes; the respondent's next autosave submits the response as it was saved.
        await SetClosesAsync(surveyId, DateTime.UtcNow.AddMinutes(-1));
        await http.PostAsync($"{responsePath}/save", Form(antiforgery, trackA, (ids["qa1"], ids["qa1a"])), cancellationToken);
        var submitted = await LatestResponseAsync(surveyId);
        var review = await http.GetStringAsync($"{responsePath}/review", cancellationToken);

        // Assert: track A's 4 points count and none was scored; track B's never count; the review agrees.
        Assert.Equal(SurveyResponseStatus.Completed, submitted.Status);
        Assert.True(submitted.AutoSubmitted);
        Assert.Equal(0m, submitted.Score);
        Assert.Equal(4m, submitted.MaxScore);
        Assert.False(submitted.Passed);
        Assert.Contains("Track A question two", review, StringComparison.Ordinal);
        Assert.DoesNotContain("Track B question one", review, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_ShowsEachAnswersFeedbackOnce()
    {
        // Arrange: the review shows right answers and feedback; question two is answered wrong.
        var (_, token, ids) = await PublishTrackQuizAsync("Track quiz feedback once", ReviewShows.Score | ReviewShows.Responses | ReviewShows.RightAnswers | ReviewShows.Feedback);
        using var http = await _fixture.CreateClientAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var (responsePath, page1) = await StartQuizAsync(http, token);
        var antiforgery = SurveysSiteFixture.Token(page1);

        await http.PostAsync(responsePath, Multipart(antiforgery, page1, "next", (ids["track"], ids["a"])), cancellationToken);
        var trackA = await http.GetStringAsync(responsePath, cancellationToken);
        await http.PostAsync(responsePath, Multipart(antiforgery, trackA, "finish", (ids["qa1"], ids["qa1a"]), (ids["qa2"], ids["qa2b"])), cancellationToken);

        // Act
        var review = await http.GetStringAsync($"{responsePath}/review", cancellationToken);

        // Assert: the question's own review template shows the feedback; the question around it doesn't repeat it.
        var start = review.IndexOf($"data-question-id=\"{ids["qa2"]}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The review lists question two.");
        var end = review.IndexOf("data-question-id=", start + 1, StringComparison.Ordinal);
        var question = end < 0 ? review[start..] : review[start..end];

        Assert.Equal(1, CountOf(question, "class=\"survey-feedback"));
        Assert.Equal(1, CountOf(question, "Incorrect"));
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;

        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private Task<(string SurveyId, string Token, Dictionary<string, string> Ids)> PublishTrackQuizAsync(string title, ReviewShows shows = ReviewShows.Score | ReviewShows.Responses)
        => _fixture.InTenantAsync(async services =>
        {
            static SurveyQuestion Choice(string id, string questionTitle)
                => new()
                {
                    Id = id,
                    Name = id,
                    ControlType = SurveyControlNames.SingleChoice,
                    Title = questionTitle,
                    Choices = [new SurveyChoice { Id = id + "a", Value = id + "a", Text = "A" }, new SurveyChoice { Id = id + "b", Value = id + "b", Text = "B" }],
                    Scoring = new SurveyQuestionScoring { Points = 2, CorrectChoiceIds = [id + "a"] },
                };

            var track = new SurveyQuestion
            {
                Id = "track",
                Name = "track",
                ControlType = SurveyControlNames.SingleChoice,
                Title = "Which track?",
                Scoring = new SurveyQuestionScoring { Points = 0 },
                Choices = [new SurveyChoice { Id = "a", Value = "a", Text = "Track A" }, new SurveyChoice { Id = "b", Value = "b", Text = "Track B" }],
            };

            var toB = new ConditionRule { Conditions = [new Condition { Source = "track", Operator = ConditionOperator.Is, Values = ["b"] }] };
            var definition = new SurveyDefinition
            {
                Title = title,
                Pages =
                [
                    new SurveyPage { Id = "p1", Name = "page_1", Title = "Track", Questions = [track], Branches = [new SurveyBranch { Id = "toB", Rule = toB, TargetKind = BranchTargetKind.Page, TargetId = "p3" }] },
                    new SurveyPage { Id = "p2", Name = "page_2", Title = "Track A", Questions = [Choice("qa1", "Track A question one"), Choice("qa2", "Track A question two")], Branches = [new SurveyBranch { Id = "done", TargetKind = BranchTargetKind.Ending, TargetId = "e1" }] },
                    new SurveyPage { Id = "p3", Name = "page_3", Title = "Track B", Questions = [Choice("qb1", "Track B question one"), Choice("qb2", "Track B question two")] },
                ],
                Endings = [new SurveyEnding { Id = "e1", Name = "thanks", Title = "Thanks", Message = "Thank you!", IsDefault = true }],
            };

            definition.Scoring.Enabled = true;
            definition.Scoring.PassMarkPercent = 60;
            definition.Review.ImmediatelyAfter = shows;
            definition.Review.AfterClose = shows;

            var manager = services.GetRequiredService<ISurveyManager>();
            var survey = await manager.CreateSurveyAsync(new SurveyCreateRequest { Title = title, Kind = SurveyKinds.Quiz, Definition = definition });
            var published = await services.GetRequiredService<ISurveyPublisher>().PublishAsync(survey.ItemId, "First version", "admin");

            Assert.True(published.Succeeded, string.Join(", ", published.Messages.Where(message => message.IsError).Select(message => message.Message)));

            var collector = await services.GetRequiredService<SurveyCollectorStore>().FindDefaultAsync(survey.ItemId);
            var reloaded = await manager.FindByIdAsync(survey.ItemId);

            // The saved draft can give questions and choices new ids, so the form posts the ids it keeps, found by name and value.
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var question in reloaded.Draft.Pages.SelectMany(page => page.Questions))
            {
                ids[question.Name] = question.Id;

                foreach (var choice in question.Choices)
                {
                    ids[choice.Value] = choice.Id;
                }
            }

            return (survey.ItemId, collector.Token, ids);
        });
}
