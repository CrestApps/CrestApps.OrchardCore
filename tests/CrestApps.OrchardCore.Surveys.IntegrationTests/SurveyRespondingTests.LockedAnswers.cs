using System.Net;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

public sealed partial class SurveyRespondingTests
{
    // Part of the refusal message without an apostrophe, which the page HTML-encodes.
    private const string LockRefusal = "the change would skip or hide a question whose answer you already checked";

    [Fact]
    public async Task LockedAnswer_AChangeThatWouldHideIt_IsRefused_AndTheScoreCountsTheKeptAnswers()
    {
        // Arrange: q2 and q3 show only when q1 is "yes"; immediate feedback locks an answer once it is checked.
        var (surveyId, token, ids) = await PublishLockedAnswerQuizAsync(branched: false);
        using var http = await _fixture.CreateClientAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var (responsePath, page) = await StartQuizAsync(http, token);
        var antiforgery = SurveysSiteFixture.Token(page);

        // The respondent checks q2 (right, so it locks) and answers q3 without checking it.
        var savedFirst = await http.PostAsync($"{responsePath}/save", Form(antiforgery, page, (ids["q1"], ids["yes"])), cancellationToken);
        var checkedAnswer = await http.PostAsync($"{responsePath}/check/{ids["q2"]}", Form(antiforgery, page, (ids["q2"], ids["q2a"])), cancellationToken);
        await http.PostAsync($"{responsePath}/save", Form(antiforgery, page, (ids["q1"], ids["yes"]), (ids["q3"], ids["q3a"])), cancellationToken);

        // Act: an autosave and a finish that change q1 to "no" (which would hide q2) are refused.
        var refusedSave = await http.PostAsync($"{responsePath}/save", Form(antiforgery, page, (ids["q1"], ids["no"])), cancellationToken);
        var refusedFinish = await http.PostAsync(responsePath, Multipart(antiforgery, page, "finish", (ids["q1"], ids["no"]), (ids["q4"], ids["q4a"])), cancellationToken);
        var refusedHtml = await refusedFinish.Content.ReadAsStringAsync(cancellationToken);
        var stillOpen = await LatestResponseAsync(surveyId);

        // The respondent keeps "yes" and finishes.
        var finished = await http.PostAsync(responsePath, Multipart(antiforgery, page, "finish", (ids["q1"], ids["yes"]), (ids["q3"], ids["q3a"]), (ids["q4"], ids["q4a"])), cancellationToken);
        var review = await http.GetStringAsync($"{responsePath}/review", cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, savedFirst.StatusCode);
        var checkedJson = await checkedAnswer.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(checkedJson.Contains("\"ok\":true", StringComparison.Ordinal), checkedJson);

        var refusedSaveJson = await refusedSave.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("\"ok\":false", refusedSaveJson, StringComparison.Ordinal);
        Assert.Contains(ids["q1"], refusedSaveJson, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, refusedFinish.StatusCode);
        Assert.Contains(LockRefusal, refusedHtml, StringComparison.Ordinal);
        Assert.Equal(SurveyResponseStatus.InProgress, stillOpen.Status);
        Assert.Equal(ids["yes"], stillOpen.Answers[ids["q1"]].Values.Single());
        Assert.Null(stillOpen.Score);

        Assert.Equal(HttpStatusCode.Redirect, finished.StatusCode);
        var response = await LatestResponseAsync(surveyId);

        Assert.Equal(SurveyResponseStatus.Completed, response.Status);
        Assert.True(response.Answers[ids["q2"]].IsLocked);
        Assert.Equal(6m, response.Score);
        Assert.Equal(6m, response.MaxScore);
        Assert.Contains("Locked question two", review, StringComparison.Ordinal);
        Assert.Contains("Unchecked question three", review, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LockedAnswer_OnABranch_BackThenChangingTheBranchAnswer_IsRefused_AndTheScoreIsUnchanged()
    {
        // Arrange: q1 "yes" goes to branch B (page 3), "no" to branch A (page 2, which jumps past page 3).
        var (surveyId, token, ids) = await PublishLockedAnswerQuizAsync(branched: true);
        using var http = await _fixture.CreateClientAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var (responsePath, page1) = await StartQuizAsync(http, token);
        var antiforgery = SurveysSiteFixture.Token(page1);

        await http.PostAsync(responsePath, Multipart(antiforgery, page1, "next", (ids["q1"], ids["yes"])), cancellationToken);
        var branchB = await http.GetStringAsync(responsePath, cancellationToken);
        var checkedAnswer = await http.PostAsync($"{responsePath}/check/{ids["q3"]}", Form(antiforgery, branchB, (ids["q3"], ids["q3a"])), cancellationToken);
        await http.PostAsync(responsePath, Multipart(antiforgery, branchB, "back"), cancellationToken);
        var backOnPage1 = await http.GetStringAsync(responsePath, cancellationToken);

        // Act: going on with "no" would leave branch B and its locked answer.
        var refused = await http.PostAsync(responsePath, Multipart(antiforgery, backOnPage1, "next", (ids["q1"], ids["no"])), cancellationToken);
        var refusedHtml = await refused.Content.ReadAsStringAsync(cancellationToken);
        var stored = await LatestResponseAsync(surveyId);

        // Assert
        var checkedJson = await checkedAnswer.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(checkedJson.Contains("\"ok\":true", StringComparison.Ordinal), checkedJson);
        Assert.Equal(PageId(page1), PageId(backOnPage1));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains(LockRefusal, refusedHtml, StringComparison.Ordinal);
        Assert.Equal(PageId(page1), PageId(refusedHtml));
        Assert.Equal(ids["yes"], stored.Answers[ids["q1"]].Values.Single());
        Assert.True(stored.Answers[ids["q3"]].IsLocked);
        Assert.Null(stored.Score);

        // The respondent stays on branch B and finishes: the score counts branch B only (q3 and q4).
        await http.PostAsync(responsePath, Multipart(antiforgery, backOnPage1, "next", (ids["q1"], ids["yes"])), cancellationToken);
        var branchBAgain = await http.GetStringAsync(responsePath, cancellationToken);
        await http.PostAsync(responsePath, Multipart(antiforgery, branchBAgain, "next"), cancellationToken);
        var lastPage = await http.GetStringAsync(responsePath, cancellationToken);
        var finished = await http.PostAsync(responsePath, Multipart(antiforgery, lastPage, "finish", (ids["q4"], ids["q4a"])), cancellationToken);
        var completed = await LatestResponseAsync(surveyId);

        Assert.Equal(HttpStatusCode.Redirect, finished.StatusCode);
        Assert.Equal(SurveyResponseStatus.Completed, completed.Status);
        Assert.Equal(4m, completed.Score);
        Assert.Equal(4m, completed.MaxScore);
    }

    private static async Task<(string ResponsePath, string Page)> StartQuizAsync(HttpClient http, string token)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var landing = await http.GetStringAsync($"s/{token}", cancellationToken);
        var started = await http.PostAsync($"s/{token}/start", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(landing),
        }), cancellationToken);

        var responsePath = started.Headers.Location?.OriginalString;

        return (responsePath, await http.GetStringAsync(responsePath, cancellationToken));
    }

    private static FormUrlEncodedContent Form(string antiforgery, string page, params (string QuestionId, string Value)[] answers)
    {
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["pageId"] = PageId(page),
        };

        foreach (var (questionId, value) in answers)
        {
            fields["q." + questionId] = value;
        }

        return new FormUrlEncodedContent(fields);
    }

    private static MultipartFormDataContent Multipart(string antiforgery, string page, string direction, params (string QuestionId, string Value)[] answers)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(antiforgery), "__RequestVerificationToken" },
            { new StringContent(PageId(page)), "pageId" },
            { new StringContent(direction), "direction" },
        };

        foreach (var (questionId, value) in answers)
        {
            content.Add(new StringContent(value), "q." + questionId);
        }

        return content;
    }

    private Task<(string SurveyId, string Token, Dictionary<string, string> Ids)> PublishLockedAnswerQuizAsync(bool branched)
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

            var whenYes = new ConditionRule { Conditions = [new Condition { Source = "q1", Operator = ConditionOperator.Is, Values = ["yes"] }] };

            var q1 = new SurveyQuestion
            {
                Id = "q1",
                Name = "q1",
                ControlType = SurveyControlNames.SingleChoice,
                Title = branched ? "Take branch B?" : "Show the extra questions?",
                Scoring = new SurveyQuestionScoring { Points = 0 },
                Choices = [new SurveyChoice { Id = "yes", Value = "yes", Text = "Yes" }, new SurveyChoice { Id = "no", Value = "no", Text = "No" }],
            };

            List<SurveyPage> pages;

            if (branched)
            {
                // Page 1 branches to page 3 on "yes"; page 2 (branch A) jumps past page 3 to page 4.
                pages =
                [
                    new SurveyPage { Id = "p1", Name = "page_1", Title = "Page 1", Questions = [q1], Branches = [new SurveyBranch { Id = "toB", Rule = whenYes, TargetKind = BranchTargetKind.Page, TargetId = "p3" }] },
                    new SurveyPage { Id = "p2", Name = "page_2", Title = "Branch A", Questions = [Choice("q2", "Branch A question", 2, "a")], Branches = [new SurveyBranch { Id = "pastB", TargetKind = BranchTargetKind.Page, TargetId = "p4" }] },
                    new SurveyPage { Id = "p3", Name = "page_3", Title = "Branch B", Questions = [Choice("q3", "Branch B question", 2, "a")] },
                    new SurveyPage { Id = "p4", Name = "page_4", Title = "Last", Questions = [Choice("q4", "Last question", 2, "a")] },
                ];
            }
            else
            {
                var showWhenYes = new SurveyVisibility { Mode = VisibilityMode.ShowWhen, Rule = whenYes };

                pages =
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
                ];
            }

            var definition = new SurveyDefinition
            {
                Title = branched ? "Branched locked answers quiz" : "Locked answers quiz",
                Pages = pages,
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
