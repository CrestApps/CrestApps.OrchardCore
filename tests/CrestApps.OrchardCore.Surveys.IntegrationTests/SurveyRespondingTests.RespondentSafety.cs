using System.Net;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// The respondent pages on a shared device and the pages around them: the resume generation cookie sits on the tenant
/// root (so a resume cookie written outside <c>/s</c> keeps the browser's other responses), kiosk responses are guest
/// responses that can't be reviewed or reopened, pages that show answers are never cached, an embed starts without a
/// form token, and the admin Details page opens.
/// </summary>
public sealed partial class SurveyRespondingTests
{
    [Fact]
    public async Task GenerationCookie_IsOnTheTenantRoot_SoPagesOutsideTheRespondentPathSeeIt()
    {
        // Arrange
        var (_, _, token) = await PublishAsync("Generation scope check");
        var (browser, cookies) = CreateBrowser();

        using (browser)
        {
            // Act
            await PostStartAsync(browser, token);

            // Assert: a course page or an admin page gets the generation, never the resume cookie itself.
            var outside = cookies.GetCookies(new Uri(_fixture.BaseAddress, "learning/surveys/item/start"));

            Assert.NotNull(outside[SurveyResumeCookies.GenerationCookie]);
            Assert.Null(outside.Cast<Cookie>().FirstOrDefault(cookie => cookie.Name.StartsWith(SurveyResumeCookies.CookiePrefix, StringComparison.Ordinal) && cookie.Name != SurveyResumeCookies.GenerationCookie));
        }
    }

    [Fact]
    public async Task AdminPreview_WritesItsResumeCookie_WithoutOrphaningTheBrowsersOtherResponse()
    {
        // Arrange: two anonymous surveys (an anonymous response has a resume token even for a signed-in user). The
        // administrator's browser has a response in progress on the first one.
        var (firstId, _, firstToken) = await PublishAsync("Preview orphan check A");
        var (secondId, _, _) = await PublishAsync("Preview orphan check B");

        await MakeAnonymousAsync(firstId);
        await MakeAnonymousAsync(secondId);

        using var admin = await _fixture.CreateClientAsync("admin");
        var cancellationToken = TestContext.Current.CancellationToken;
        var responsePath = await PostStartAsync(admin, firstToken);

        // Act: a preview of the second survey writes its resume cookie from the admin path.
        var previewPage = await admin.GetStringAsync($"Admin/surveys/{secondId}/preview", cancellationToken);
        var preview = await admin.PostAsync($"Admin/surveys/{secondId}/preview", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(previewPage),
            ["version"] = "published",
        }), cancellationToken);
        var previewResponse = await admin.GetAsync(preview.Headers.Location?.OriginalString, cancellationToken);
        var firstAgain = await admin.GetAsync(responsePath, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, preview.StatusCode);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstAgain.StatusCode);
    }

    [Fact]
    public async Task Kiosk_SignedInDevice_StartsGuestResponses_ThatCannotBeReviewedOrReopened()
    {
        // Arrange: a kiosk whose tablet stays signed in.
        var (surveyId, questionId, token) = await PublishAsync("Kiosk check");

        await MakeKioskAsync(surveyId);

        using var device = await _fixture.CreateClientAsync(SurveysSiteFixture.RespondentUserName);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act: the first person starts and walks away; the second person starts from the kiosk page.
        var first = await PostStartAsync(device, token);
        var firstStored = await LatestResponseAsync(surveyId);
        var second = await PostStartAsync(device, token);

        // The second person finishes, and the next one tries the review and a reopen before the ending clears it.
        var page = await device.GetStringAsync(second, cancellationToken);
        var finished = await device.PostAsync(second, new MultipartFormDataContent
        {
            { new StringContent(SurveysSiteFixture.Token(page)), "__RequestVerificationToken" },
            { new StringContent(PageId(page)), "pageId" },
            { new StringContent("finish"), "direction" },
            { new StringContent("All good"), $"q.{questionId}" },
        }, cancellationToken);
        var review = await device.GetAsync(second + "/review", cancellationToken);
        var reopen = await device.PostAsync(second + "/reopen", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(page),
        }), cancellationToken);

        // Assert
        Assert.Null(firstStored.UserId);
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.Redirect, finished.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, review.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reopen.StatusCode);
    }

    [Fact]
    public async Task PagesThatShowAnswers_AreNeverCached()
    {
        // Arrange: a completed response. The ending, the review and the totals show answers or scores whether or not
        // the survey lets this respondent see them, so every one of them is checked.
        var (_, questionId, token) = await PublishAsync("Cache check");
        var (browser, _) = CreateBrowser();
        var cancellationToken = TestContext.Current.CancellationToken;

        using (browser)
        {
            var responsePath = await PostStartAsync(browser, token);
            var page = await browser.GetStringAsync(responsePath, cancellationToken);
            await browser.PostAsync(responsePath, new MultipartFormDataContent
            {
                { new StringContent(SurveysSiteFixture.Token(page)), "__RequestVerificationToken" },
                { new StringContent(PageId(page)), "pageId" },
                { new StringContent("finish"), "direction" },
                { new StringContent("Fine"), $"q.{questionId}" },
            }, cancellationToken);

            // Act
            var pages = new[] { responsePath + "/done", responsePath + "/review", responsePath + "/results", $"s/{token}/results", $"s/{token}" };
            var results = await Task.WhenAll(pages.Select(path => browser.GetAsync(path, cancellationToken)));

            // Assert
            Assert.All(results, result =>
            {
                Assert.True(result.Headers.CacheControl?.NoStore == true, $"{result.RequestMessage?.RequestUri} sends no Cache-Control: no-store.");
            });
        }
    }

    [Fact]
    public async Task Landing_WithAutoStart_PostsItsStartFormAtOnce()
    {
        // Arrange: the Start button of an embedded survey links here, so the cached embed holds no form token.
        var (_, _, token) = await PublishAsync("Embed start check");
        var (browser, _) = CreateBrowser();

        using (browser)
        {
            // Act
            var plain = await browser.GetStringAsync($"s/{token}", TestContext.Current.CancellationToken);
            var autoStart = await browser.GetStringAsync($"s/{token}?autostart=1", TestContext.Current.CancellationToken);

            // Assert
            Assert.DoesNotContain("data-survey-autostart=\"true\"", plain, StringComparison.Ordinal);
            Assert.Contains("data-survey-autostart=\"true\"", autoStart, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AdminDetails_OfAResponse_Opens()
    {
        // Arrange: one response in progress and one completed.
        var (_, questionId, token) = await PublishAsync("Details check");
        var (browser, _) = CreateBrowser();
        var cancellationToken = TestContext.Current.CancellationToken;

        using (browser)
        {
            var inProgress = await ResponseOfPathAsync(await PostStartAsync(browser, token));
            string completedPath;

            var (other, _) = CreateBrowser();

            using (other)
            {
                completedPath = await PostStartAsync(other, token);
                var page = await other.GetStringAsync(completedPath, cancellationToken);
                await other.PostAsync(completedPath, new MultipartFormDataContent
                {
                    { new StringContent(SurveysSiteFixture.Token(page)), "__RequestVerificationToken" },
                    { new StringContent(PageId(page)), "pageId" },
                    { new StringContent("finish"), "direction" },
                    { new StringContent("Detailed answer"), $"q.{questionId}" },
                }, cancellationToken);
            }

            var completed = await ResponseOfPathAsync(completedPath);
            using var admin = await _fixture.CreateClientAsync("admin");

            // Act
            var inProgressPage = await admin.GetAsync($"Admin/surveys/responses/{inProgress.ItemId}", cancellationToken);
            var completedPage = await admin.GetAsync($"Admin/surveys/responses/{completed.ItemId}", cancellationToken);

            // Assert
            Assert.NotEqual(inProgress.ItemId, completed.ItemId);
            Assert.Equal(SurveyResponseStatus.Completed, completed.Status);
            Assert.Equal(HttpStatusCode.OK, inProgressPage.StatusCode);
            Assert.Equal(HttpStatusCode.OK, completedPage.StatusCode);
            var completedHtml = await completedPage.Content.ReadAsStringAsync(cancellationToken);

            Assert.Contains(completed.Number, completedHtml, StringComparison.Ordinal);
            Assert.Contains("Detailed answer", completedHtml, StringComparison.Ordinal);
        }
    }

    private Task<SurveyResponse> ResponseOfPathAsync(string responsePath)
        => _fixture.InTenantAsync(services => services.GetRequiredService<SurveyResponseStore>()
            .FindByIdAsync(responsePath.Substring(responsePath.LastIndexOf('/') + 1)).AsTask());

    private Task MakeAnonymousAsync(string surveyId)
        => _fixture.InTenantAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyStore>();
            var survey = await store.FindByIdAsync(surveyId);

            survey.Access ??= new SurveyAccessSettings();
            survey.Access.Anonymity = AnonymityMode.Anonymous;

            await store.UpdateAsync(survey);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync();
        });

    private Task MakeKioskAsync(string surveyId)
        => _fixture.InTenantAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyCollectorStore>();
            var collector = await store.FindDefaultAsync(surveyId);

            collector.Kind = SurveyCollectorKinds.Kiosk;

            await store.UpdateAsync(collector);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync();
        });
}
