using System.Net;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Round 12 of the review: the results gate checks the survey of the response, respondents see the published version,
/// a refused reopen says why, an invitation start keeps its collector's language, and author HTML shows as markup.
/// </summary>
public sealed partial class SurveyRespondingTests
{
    private const string ResultsUnavailable = "The results of this survey are not available.";

    [Fact]
    public async Task Results_AfterAnswering_RefuseTheCompletedResponseOfAnotherSurvey()
    {
        // Arrange: R12-L2, the respondent answered survey B and copies B's resume cookie under survey A's name.
        var (surveyA, _, tokenA) = await PublishAsync("Results gate A", definition => definition.Review.ResultsVisibility = ResultsVisibility.AfterAnswer);
        var (surveyB, questionB, tokenB) = await PublishAsync("Results gate B", definition => definition.Review.ResultsVisibility = ResultsVisibility.AfterAnswer);
        var cancellationToken = TestContext.Current.CancellationToken;
        var (browser, cookies) = CreateBrowser();

        using (browser)
        {
            await CompleteAsync(browser, tokenB, questionB, "Fine");

            var copied = cookies.GetCookies(new Uri(_fixture.BaseAddress, "s/r/any"))["sr_" + surveyB];
            Assert.NotNull(copied);
            cookies.Add(new Uri(_fixture.BaseAddress, "s/"), new Cookie("sr_" + surveyA, copied.Value, "/s"));

            // Act
            var other = await browser.GetStringAsync($"s/{tokenA}/results", cancellationToken);
            var own = await browser.GetStringAsync($"s/{tokenB}/results", cancellationToken);

            // Assert
            Assert.Contains(ResultsUnavailable, other, StringComparison.Ordinal);
            Assert.DoesNotContain(ResultsUnavailable, own, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task MyResponsesAndLanding_ShowThePublishedVersion_NotTheDraft()
    {
        // Arrange: R12-L9, the author changed the title and the CSS class of the draft after publishing.
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (surveyId, questionId, token) = await PublishAsync("Published title " + suffix, definition => definition.Presentation.CssClass = "published-class-" + suffix);
        await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<ISurveyManager>();
            var survey = await manager.FindByIdAsync(surveyId);
            var draft = survey.Draft;

            draft.Title = "Draft title " + suffix;
            draft.Presentation.CssClass = "draft-class-" + suffix;
            await manager.SaveDraftAsync(survey, draft);
        });

        using var respondent = await _fixture.CreateClientAsync(SurveysSiteFixture.RespondentUserName);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var landing = await respondent.GetStringAsync($"s/{token}", cancellationToken);
        await CompleteAsync(respondent, token, questionId, "Fine");
        var mine = await respondent.GetStringAsync("surveys/my", cancellationToken);

        // Assert
        Assert.Contains("published-class-" + suffix, landing, StringComparison.Ordinal);
        Assert.DoesNotContain("draft-class-" + suffix, landing, StringComparison.Ordinal);
        Assert.Contains("Published title " + suffix, mine, StringComparison.Ordinal);
        Assert.DoesNotContain("Draft title " + suffix, mine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reopen_WhenTheSurveyDoesNotAllowEditsAfterSubmit_SaysWhyOnTheDonePage()
    {
        // Arrange: R12-L10, the survey doesn't allow changes after submitting.
        var (_, questionId, token) = await PublishAsync("Reopen refusal");
        var cancellationToken = TestContext.Current.CancellationToken;
        var (browser, _) = CreateBrowser();

        using (browser)
        {
            var responsePath = await CompleteAsync(browser, token, questionId, "Fine");
            var landing = await browser.GetStringAsync($"s/{token}", cancellationToken);

            // Act
            var reopened = await browser.PostAsync(responsePath + "/reopen", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = SurveysSiteFixture.Token(landing),
            }), cancellationToken);
            var page = await FollowAsync(browser, reopened);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, reopened.StatusCode);
            Assert.Contains("This response can no longer be changed.", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task InvitationStart_UsesTheLanguageOfItsCollector()
    {
        // Arrange: R12-L11, the invitation collector is set to French; the browser asks for nothing.
        var (surveyId, _, _) = await PublishAsync("Invitation language");
        var invitation = await _fixture.InTenantAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest
        {
            SurveyId = surveyId,
            Email = "french@example.com",
        }));
        await _fixture.InTenantAsync(async services =>
        {
            var collectors = services.GetRequiredService<SurveyCollectorStore>();
            var collector = await collectors.FindByIdAsync(invitation.Invitation.CollectorId);

            collector.Culture = "fr-FR";
            await collectors.UpdateAsync(collector);
        });

        var cancellationToken = TestContext.Current.CancellationToken;
        var (browser, _) = CreateBrowser();
        var path = invitation.Link.Substring(invitation.Link.IndexOf("s/i/", StringComparison.Ordinal));

        using (browser)
        {
            // Act
            var page = await browser.GetStringAsync(path, cancellationToken);
            var started = await browser.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = SurveysSiteFixture.Token(page),
            }), cancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
            Assert.Equal("fr-FR", (await LatestResponseAsync(surveyId)).Culture);
        }
    }

    [Fact]
    public async Task ReviewAndResults_ShowTheAuthorsHtmlAsSanitizedMarkup()
    {
        // Arrange: R12-L19, a question whose title and general feedback are the author's HTML, with a script that must go.
        var (surveyId, questionId, token) = await PublishAsync("Html review", definition =>
        {
            definition.Pages[0].Questions.Add(new SurveyQuestion
            {
                Id = "q2",
                Name = "pick",
                ControlType = SurveyControlNames.SingleChoice,
                Title = "Pick <em>one</em><script>alert(1)</script>",
                Choices = [new SurveyChoice { Id = "a", Value = "a", Text = "A" }, new SurveyChoice { Id = "b", Value = "b", Text = "B" }],
                Scoring = new SurveyQuestionScoring { Points = 1, CorrectChoiceIds = ["a"], GeneralFeedback = "See <strong>chapter 2</strong><script>alert(2)</script>" },
            });
            definition.Scoring.Enabled = true;
            definition.Review.ImmediatelyAfter = ReviewShows.Responses | ReviewShows.GeneralFeedback;
            definition.Review.ResultsVisibility = ResultsVisibility.Always;
        });
        var pick = await _fixture.InTenantAsync(async services => (await services.GetRequiredService<ISurveyManager>().FindByIdAsync(surveyId)).Draft.Pages[0].Questions.Single(question => question.Name == "pick"));
        var cancellationToken = TestContext.Current.CancellationToken;
        var (browser, _) = CreateBrowser();

        using (browser)
        {
            var responsePath = await CompleteAsync(browser, token, questionId, "Fine", ($"q.{pick.Id}", pick.Choices.Single(choice => choice.Value == "b").Id));

            // Act
            var review = await browser.GetStringAsync(responsePath + "/review", cancellationToken);
            var results = await browser.GetStringAsync($"s/{token}/results", cancellationToken);

            // Assert
            Assert.Contains("See <strong>chapter 2</strong>", review, StringComparison.Ordinal);
            Assert.DoesNotContain("&lt;strong&gt;", review, StringComparison.Ordinal);
            Assert.DoesNotContain("alert(2)", review, StringComparison.Ordinal);
            Assert.Contains("Pick <em>one</em>", results, StringComparison.Ordinal);
            Assert.DoesNotContain("&lt;em&gt;", results, StringComparison.Ordinal);
            Assert.DoesNotContain("alert(1)", results, StringComparison.Ordinal);
        }
    }

    // Starts a response on a public link and finishes it with one answer. Returns the response path.
    private static async Task<string> CompleteAsync(HttpClient browser, string token, string questionId, string answer, params (string Name, string Value)[] more)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var responsePath = await PostStartAsync(browser, token);
        var page = await browser.GetStringAsync(responsePath, cancellationToken);
        var form = new MultipartFormDataContent
        {
            { new StringContent(SurveysSiteFixture.Token(page)), "__RequestVerificationToken" },
            { new StringContent(PageId(page)), "pageId" },
            { new StringContent("finish"), "direction" },
            { new StringContent(answer), $"q.{questionId}" },
        };

        foreach (var (name, value) in more)
        {
            form.Add(new StringContent(value), name);
        }

        var submitted = await browser.PostAsync(responsePath, form, cancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);

        return responsePath;
    }

    // Follows redirects (at most five) and returns the final page.
    private static async Task<string> FollowAsync(HttpClient browser, HttpResponseMessage response)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        for (var hop = 0; hop < 5 && response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther; hop++)
        {
            response = await browser.GetAsync(response.Headers.Location, cancellationToken);
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
