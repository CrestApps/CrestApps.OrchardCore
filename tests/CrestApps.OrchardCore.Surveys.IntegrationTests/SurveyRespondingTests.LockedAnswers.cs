using System.Net;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

public sealed partial class SurveyRespondingTests
{
    [Fact]
    public async Task LockedAnswer_HiddenLater_CountsInTheScore_AndShowsOnTheReview()
    {
        // Arrange: q2 and q3 show only when q1 is "yes"; immediate feedback locks an answer once it is checked.
        var (surveyId, token, ids) = await PublishLockedAnswerQuizAsync();
        using var http = await _fixture.CreateClientAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var landing = await http.GetStringAsync($"s/{token}", cancellationToken);
        var started = await http.PostAsync($"s/{token}/start", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(landing),
        }), cancellationToken);

        var responsePath = started.Headers.Location?.OriginalString;
        var page = await http.GetStringAsync(responsePath, cancellationToken);
        var antiforgery = SurveysSiteFixture.Token(page);

        // Act: the respondent checks q2 (right, so it locks), answers q3 without checking it, then changes q1 to
        // "no", which hides both, and finishes.
        var savedFirst = await http.PostAsync($"{responsePath}/save", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["pageId"] = PageId(page),
            ["q." + ids["q1"]] = ids["yes"],
        }), cancellationToken);

        var checkedAnswer = await http.PostAsync($"{responsePath}/check/{ids["q2"]}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["pageId"] = PageId(page),
            ["q." + ids["q1"]] = ids["yes"],
            ["q." + ids["q2"]] = ids["q2a"],
        }), cancellationToken);

        var saved = await http.PostAsync($"{responsePath}/save", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["pageId"] = PageId(page),
            ["q." + ids["q1"]] = ids["yes"],
            ["q." + ids["q2"]] = ids["q2a"],
            ["q." + ids["q3"]] = ids["q3a"],
        }), cancellationToken);

        var finished = await http.PostAsync(responsePath, new MultipartFormDataContent
        {
            { new StringContent(antiforgery), "__RequestVerificationToken" },
            { new StringContent(PageId(page)), "pageId" },
            { new StringContent("finish"), "direction" },
            { new StringContent(ids["no"]), "q." + ids["q1"] },
            { new StringContent(ids["q4a"]), "q." + ids["q4"] },
        }, cancellationToken);

        var review = await http.GetAsync($"{responsePath}/review", cancellationToken);
        var reviewHtml = await review.Content.ReadAsStringAsync(cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, savedFirst.StatusCode);
        Assert.Equal(HttpStatusCode.OK, checkedAnswer.StatusCode);
        Assert.Contains("\"ok\":true", await checkedAnswer.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, finished.StatusCode);
        Assert.EndsWith("/done", finished.Headers.Location?.OriginalString, StringComparison.Ordinal);

        var response = await LatestResponseAsync(surveyId);

        Assert.Equal(SurveyResponseStatus.Completed, response.Status);
        Assert.True(response.Answers[ids["q2"]].IsLocked);
        Assert.Equal(3m, response.Score);
        Assert.Equal(3m, response.MaxScore);

        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.Contains("Locked question two", reviewHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Unchecked question three", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("Always shown question four", reviewHtml, StringComparison.Ordinal);
    }

    private Task<(string SurveyId, string Token, Dictionary<string, string> Ids)> PublishLockedAnswerQuizAsync()
        => _fixture.InTenantAsync(async services =>
        {
            static SurveyQuestion Choice(string id, string title, decimal points, string correct, SurveyVisibility visibility = null)
                => new()
                {
                    Id = id,
                    Name = id,
                    ControlType = SurveyControlNames.SingleChoice,
                    Title = title,
                    Choices = [new SurveyChoice { Id = id + "a", Value = id + "a", Text = "A" }, new SurveyChoice { Id = id + "b", Value = id + "b", Text = "B" }],
                    Scoring = new SurveyQuestionScoring { Points = points, CorrectChoiceIds = [id + correct] },
                    Visibility = visibility ?? new SurveyVisibility(),
                };

            var showWhenYes = new SurveyVisibility
            {
                Mode = VisibilityMode.ShowWhen,
                Rule = new ConditionRule { Conditions = [new Condition { Source = "q1", Operator = ConditionOperator.Is, Values = ["yes"] }] },
            };

            var q1 = new SurveyQuestion
            {
                Id = "q1",
                Name = "q1",
                ControlType = SurveyControlNames.SingleChoice,
                Title = "Show the extra questions?",
                Scoring = new SurveyQuestionScoring { Points = 0 },
                Choices = [new SurveyChoice { Id = "yes", Value = "yes", Text = "Yes" }, new SurveyChoice { Id = "no", Value = "no", Text = "No" }],
            };

            var definition = new SurveyDefinition
            {
                Title = "Locked answers quiz",
                Pages =
                [
                    new SurveyPage
                    {
                        Id = "p1",
                        Name = "page_1",
                        Title = "Page 1",
                        Questions =
                        [
                            q1,
                            Choice("q2", "Locked question two", 2, "a", showWhenYes),
                            Choice("q3", "Unchecked question three", 3, "a", showWhenYes),
                            Choice("q4", "Always shown question four", 1, "a"),
                        ],
                    },
                ],
                Endings = [new SurveyEnding { Id = "e1", Name = "thanks", Title = "Thanks", Message = "Thank you!", IsDefault = true }],
            };

            definition.Scoring.Enabled = true;
            definition.Scoring.FeedbackMode = FeedbackMode.Immediate;

            var manager = services.GetRequiredService<ISurveyManager>();
            var survey = await manager.CreateSurveyAsync(new SurveyCreateRequest { Title = definition.Title, Kind = SurveyKinds.Quiz, Definition = definition });
            var published = await services.GetRequiredService<ISurveyPublisher>().PublishAsync(survey.ItemId, "First version", "admin");

            Assert.True(published.Succeeded, string.Join(", ", published.Messages.Where(message => message.IsError).Select(message => message.Message)));

            var collector = await services.GetRequiredService<SurveyCollectorStore>().FindDefaultAsync(survey.ItemId);
            var reloaded = await manager.FindByIdAsync(survey.ItemId);

            // The saved draft can give questions and choices new ids, so the form posts the ids it keeps, found by name and value.
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var question in reloaded.Draft.Pages[0].Questions)
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
