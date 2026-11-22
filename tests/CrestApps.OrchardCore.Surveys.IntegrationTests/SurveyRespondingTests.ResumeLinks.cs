using System.Net;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Resume links in two browsers (two clients with their own cookies): a link's token is traded for a new one and
/// leaves the address, it resumes once, and it never takes over a browser's own response in progress.
/// </summary>
public sealed partial class SurveyRespondingTests
{
    [Fact]
    public async Task ResumeLink_OpenedInAnotherBrowser_TheSharerCanNoLongerReadIt()
    {
        // Arrange: browser A starts a response on the public link and shares its own resume link.
        var (surveyId, questionId, token) = await PublishAsync("Fixation check");
        var cancellationToken = TestContext.Current.CancellationToken;
        var (browserA, cookiesA) = CreateBrowser();
        var (browserB, _) = CreateBrowser();
        var (browserC, _) = CreateBrowser();

        using (browserA)
        using (browserB)
        using (browserC)
        {
            var responsePath = await PostStartAsync(browserA, token);
            var responseId = responsePath.Substring(responsePath.LastIndexOf('/') + 1);
            var sharedLink = $"s/r/{responseId}?t={Uri.EscapeDataString(ResumeTokenOf(cookiesA, surveyId))}";

            // Act: browser B opens the shared link, then answers through the public link.
            var claimed = await browserB.GetAsync(sharedLink, cancellationToken);
            var restarted = await PostStartAsync(browserB, token);
            var page = await browserB.GetStringAsync(restarted, cancellationToken);
            var saved = await browserB.PostAsync(restarted + "/save", new MultipartFormDataContent
            {
                { new StringContent(SurveysSiteFixture.Token(page)), "__RequestVerificationToken" },
                { new StringContent(PageId(page)), "pageId" },
                { new StringContent("my phone is 555-0100"), $"q.{questionId}" },
            }, cancellationToken);

            var sharerReloads = await browserA.GetAsync($"s/r/{responseId}", cancellationToken);
            var linkAgain = await browserC.GetAsync(sharedLink, cancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, claimed.StatusCode);
            Assert.Equal($"/s/r/{responseId}", claimed.Headers.Location?.OriginalString);
            Assert.True(claimed.Headers.TryGetValues("Referrer-Policy", out var policy));
            Assert.Equal("no-referrer", policy.Single());
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

            // The sharer's token stopped working when B traded it, so A cannot read what B typed.
            Assert.Equal(HttpStatusCode.NotFound, sharerReloads.StatusCode);
            Assert.DoesNotContain("555-0100", await sharerReloads.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);

            // A resume link resumes once.
            Assert.Equal(HttpStatusCode.NotFound, linkAgain.StatusCode);
        }
    }

    [Fact]
    public async Task ResumeLink_NeverReplacesTheBrowsersOwnResponseInProgress()
    {
        // Arrange: browser A and browser B each start their own response.
        var (surveyId, _, token) = await PublishAsync("Own response check");
        var cancellationToken = TestContext.Current.CancellationToken;
        var (browserA, cookiesA) = CreateBrowser();
        var (browserB, _) = CreateBrowser();

        using (browserA)
        using (browserB)
        {
            var responseOfA = await PostStartAsync(browserA, token);
            var responseOfB = await PostStartAsync(browserB, token);
            var linkOfA = $"{responseOfA.TrimStart('/')}?t={Uri.EscapeDataString(ResumeTokenOf(cookiesA, surveyId))}";

            // Act: B opens A's link, then starts again on the public link.
            var opened = await browserB.GetAsync(linkOfA, cancellationToken);
            var restarted = await PostStartAsync(browserB, token);
            var stillA = await browserA.GetAsync(responseOfA, cancellationToken);

            // Assert
            Assert.NotEqual(responseOfA, responseOfB);
            Assert.Equal(HttpStatusCode.Conflict, opened.StatusCode);
            Assert.Equal(responseOfB, restarted);
            Assert.Equal(HttpStatusCode.OK, stillA.StatusCode);
        }
    }

    [Fact]
    public async Task InvitationLink_OpenedByAScanner_StartsNothing_AndTheInviteeStartsWithAPost()
    {
        // Arrange
        var (surveyId, _, _) = await PublishAsync("Scanner check");
        var invitation = await _fixture.InTenantAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest
        {
            SurveyId = surveyId,
            Email = "scanned@example.com",
        }));

        var cancellationToken = TestContext.Current.CancellationToken;
        var (scanner, _) = CreateBrowser();
        var (invitee, _) = CreateBrowser();
        var path = invitation.Link.Substring(invitation.Link.IndexOf("s/i/", StringComparison.Ordinal));

        using (scanner)
        using (invitee)
        {
            // Act
            var scanned = await scanner.GetAsync(path, cancellationToken);
            var afterScan = await InvitationAsync(invitation.Invitation.ItemId);
            var page = await invitee.GetStringAsync(path, cancellationToken);
            var started = await invitee.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = SurveysSiteFixture.Token(page),
            }), cancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, scanned.StatusCode);
            Assert.Equal(SurveyInvitationStatus.Opened, afterScan.Status);
            Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
            Assert.StartsWith("/s/r/", started.Headers.Location?.OriginalString, StringComparison.Ordinal);
            Assert.Equal(SurveyInvitationStatus.Started, (await InvitationAsync(invitation.Invitation.ItemId)).Status);
        }
    }

    private (HttpClient Client, CookieContainer Cookies) CreateBrowser()
    {
        var cookies = new CookieContainer();
        var client = new HttpClient(new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false })
        {
            BaseAddress = _fixture.BaseAddress,
        };

        return (client, cookies);
    }

    private static async Task<string> PostStartAsync(HttpClient browser, string collectorToken)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var landing = await browser.GetStringAsync($"s/{collectorToken}", cancellationToken);
        var started = await browser.PostAsync($"s/{collectorToken}/start", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(landing),
        }), cancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);

        return started.Headers.Location?.OriginalString;
    }

    // The resume token the browser holds for a survey: what a respondent would copy as their own resume link.
    private string ResumeTokenOf(CookieContainer cookies, string surveyId)
    {
        var cookie = cookies.GetCookies(new Uri(_fixture.BaseAddress, "s/r/any"))["sr_" + surveyId];

        Assert.NotNull(cookie);

        var value = Uri.UnescapeDataString(cookie.Value);

        return value.Substring(0, value.LastIndexOf('~'));
    }

    private Task<SurveyInvitation> InvitationAsync(string invitationId)
        => _fixture.InTenantAsync(services => services.GetRequiredService<SurveyInvitationStore>().FindByIdAsync(invitationId).AsTask());
}
