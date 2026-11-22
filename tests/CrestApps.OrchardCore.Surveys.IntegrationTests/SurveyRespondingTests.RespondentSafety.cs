using System.Net;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls.Hidden;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

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

            Assert.NotNull(outside[SurveyResumeCookies.GenerationCookieName(ShellSettings.DefaultShellName)]);
            Assert.Null(outside.Cast<Cookie>().FirstOrDefault(cookie => cookie.Name.StartsWith(SurveyResumeCookies.CookiePrefix, StringComparison.Ordinal) && cookie.Name != SurveyResumeCookies.GenerationCookieName(ShellSettings.DefaultShellName)));
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
    public async Task Kiosk_SignedInVisitor_IsRefused_SoTheKioskCannotBypassPerPersonLimits()
    {
        // Arrange: a kiosk records uncounted guest responses, so a signed-in visitor (the kiosk address opened at home)
        // would bypass the survey's per-person limits.
        var (surveyId, _, token) = await PublishAsync("Kiosk signed-in check");
        var (_, _, otherToken) = await PublishAsync("Kiosk signed-in form token source");

        await MakeKioskAsync(surveyId);

        using var visitor = await _fixture.CreateClientAsync(SurveysSiteFixture.RespondentUserName);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act: the kiosk page, then a start posted anyway (with the visitor's form token from another survey's page).
        var landing = await visitor.GetStringAsync($"s/{token}", cancellationToken);
        var formToken = SurveysSiteFixture.Token(await visitor.GetStringAsync($"s/{otherToken}", cancellationToken));
        var started = await visitor.PostAsync($"s/{token}/start", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = formToken,
        }), cancellationToken);
        var startedPage = await started.Content.ReadAsStringAsync(cancellationToken);
        var count = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ISurveyResponseQueries>().ListAsync(new SurveyResponseQuery { SurveyIds = [surveyId], Take = 1 })).Count);

        // Assert: no start form, a message instead, and the post starts nothing.
        Assert.DoesNotContain($"s/{token}/start", landing, StringComparison.Ordinal);
        Assert.Contains("Sign out", landing, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Contains("Sign out", startedPage, StringComparison.Ordinal);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Kiosk_StartsGuestResponses_ThatCannotBeReviewedOrReopened()
    {
        // Arrange: a kiosk tablet that is signed out.
        var (surveyId, questionId, token) = await PublishAsync("Kiosk check");

        await MakeKioskAsync(surveyId);

        var (device, _) = CreateBrowser();
        using var deviceScope = device;
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
    public async Task Landing_WithAutoStart_PostsItsStartFormAtOnce_OnlyFromAPageOfTheSameSite()
    {
        // Arrange: the Start button of an embedded survey links here, so the cached embed holds no form token. A link
        // on another site, in an e-mail or typed in must not skip the introduction or start a response.
        var (_, _, token) = await PublishAsync("Embed start check");
        var (browser, _) = CreateBrowser();
        var cancellationToken = TestContext.Current.CancellationToken;

        using (browser)
        {
            // Act
            var plain = await browser.GetStringAsync($"s/{token}", cancellationToken);
            var sameSite = await GetLandingAsync(browser, $"s/{token}?autostart=1", "same-origin");
            var crossSite = await GetLandingAsync(browser, $"s/{token}?autostart=1", "cross-site");
            var typedIn = await GetLandingAsync(browser, $"s/{token}?autostart=1", "none");
            var noHeader = await GetLandingAsync(browser, $"s/{token}?autostart=1", fetchSite: null);

            // Assert
            Assert.DoesNotContain("data-survey-autostart=\"true\"", plain, StringComparison.Ordinal);
            Assert.Contains("data-survey-autostart=\"true\"", await sameSite.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
            Assert.DoesNotContain("data-survey-autostart=\"true\"", await crossSite.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
            Assert.DoesNotContain("data-survey-autostart=\"true\"", await typedIn.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
            Assert.DoesNotContain("data-survey-autostart=\"true\"", await noHeader.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
            Assert.Contains("noindex", sameSite.Headers.GetValues("X-Robots-Tag").Single(), StringComparison.Ordinal);
        }
    }

    private static async Task<HttpResponseMessage> GetLandingAsync(HttpClient browser, string path, string fetchSite)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (fetchSite is not null)
        {
            request.Headers.Add("Sec-Fetch-Site", fetchSite);
        }

        var response = await browser.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return response;
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

    [Fact]
    public async Task RespondentReview_LeavesOutHiddenQuestions_TheAdminDetailsPageShowsThem()
    {
        // Arrange: a hidden question records a value the respondent never saw nor typed (from the link, a consumer or a
        // workflow); here the variable's default.
        const string HiddenValue = "campaign-value-7d3f";
        var (surveyId, token) = await _fixture.InTenantAsync(async services =>
        {
            var hidden = new SurveyQuestion { Id = "q2", Name = "source_value", ControlType = SurveyControlNames.Hidden, Title = "Source" };

            hidden.SetSettings(new HiddenSettings { VariableName = "source" });

            var manager = services.GetRequiredService<ISurveyManager>();
            var survey = await manager.CreateSurveyAsync(new SurveyCreateRequest
            {
                Title = "Hidden review check",
                Definition = new SurveyDefinition
                {
                    Title = "Hidden review check",
                    Variables = [new SurveyVariable { Name = "source", Kind = VariableKind.Hidden, DefaultValue = HiddenValue }],
                    Pages =
                    [
                        new SurveyPage
                        {
                            Id = "p1",
                            Name = "page_1",
                            Title = "Page 1",
                            Questions =
                            [
                                new SurveyQuestion { Id = "q1", Name = "comment", ControlType = SurveyControlNames.ShortText, Title = "What did you think?", Required = true },
                                hidden,
                            ],
                        },
                    ],
                    Endings = [new SurveyEnding { Id = "e1", Name = "thanks", Title = "Thanks", Message = "Thank you!", IsDefault = true }],
                },
            });

            var published = await services.GetRequiredService<ISurveyPublisher>().PublishAsync(survey.ItemId, "First version", "admin");

            Assert.True(published.Succeeded, string.Join(", ", published.Messages.Where(message => message.IsError).Select(message => message.Message)));

            return (survey.ItemId, (await services.GetRequiredService<SurveyCollectorStore>().FindDefaultAsync(survey.ItemId)).Token);
        });

        var (browser, _) = CreateBrowser();
        var cancellationToken = TestContext.Current.CancellationToken;

        using (browser)
        {
            var responsePath = await PostStartAsync(browser, token);
            var page = await browser.GetStringAsync(responsePath, cancellationToken);
            var firstQuestion = await _fixture.InTenantAsync(async services => (await services.GetRequiredService<SurveyStore>().FindByIdAsync(surveyId)).Draft.Pages[0].Questions[0].Id);

            await browser.PostAsync(responsePath, new MultipartFormDataContent
            {
                { new StringContent(SurveysSiteFixture.Token(page)), "__RequestVerificationToken" },
                { new StringContent(PageId(page)), "pageId" },
                { new StringContent("finish"), "direction" },
                { new StringContent("Fine"), $"q.{firstQuestion}" },
            }, cancellationToken);

            var completed = await ResponseOfPathAsync(responsePath);
            using var admin = await _fixture.CreateClientAsync("admin");

            // Act
            var review = await browser.GetAsync(responsePath + "/review", cancellationToken);
            var details = await admin.GetStringAsync($"Admin/surveys/responses/{completed.ItemId}", cancellationToken);
            var reviewHtml = await review.Content.ReadAsStringAsync(cancellationToken);

            // Assert
            Assert.Equal(SurveyResponseStatus.Completed, completed.Status);
            Assert.Equal(HttpStatusCode.OK, review.StatusCode);
            Assert.Contains("Fine", reviewHtml, StringComparison.Ordinal);
            Assert.DoesNotContain(HiddenValue, reviewHtml, StringComparison.Ordinal);
            Assert.Contains(HiddenValue, details, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Captcha_IsSkippedOnlyByAnInvitationSentToSomeone_NotByABarePersonalLink()
    {
        // Arrange: a survey that requires a CAPTCHA (the reCAPTCHA feature is off here, so a respondent who must pass
        // one is refused), an e-mail invitation and a bare personal link made for no one.
        var (surveyId, _, _) = await PublishAsync("Invitation CAPTCHA check");

        await _fixture.InTenantAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyStore>();
            var survey = await store.FindByIdAsync(surveyId);

            survey.Access ??= new SurveyAccessSettings();
            survey.Access.RequireCaptcha = true;

            await store.UpdateAsync(survey);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync();
        });

        var invited = await _fixture.InTenantAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest
        {
            SurveyId = surveyId,
            Email = "captcha-invitee@example.com",
        }));
        var bare = await _fixture.InTenantAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest
        {
            SurveyId = surveyId,
        }, requireRecipient: false));

        // Act
        var invitedStart = await PostInvitationAsync(invited.Link);
        var bareStart = await PostInvitationAsync(bare.Link);

        // Assert
        Assert.True(invited.Succeeded);
        Assert.True(bare.Succeeded);
        Assert.Equal(HttpStatusCode.Redirect, invitedStart.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bareStart.StatusCode);
    }

    private async Task<HttpResponseMessage> PostInvitationAsync(string link)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (browser, _) = CreateBrowser();

        using (browser)
        {
            var path = link.Substring(link.IndexOf("s/i/", StringComparison.Ordinal));
            var page = await browser.GetStringAsync(path, cancellationToken);

            return await browser.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = SurveysSiteFixture.Token(page),
            }), cancellationToken);
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
