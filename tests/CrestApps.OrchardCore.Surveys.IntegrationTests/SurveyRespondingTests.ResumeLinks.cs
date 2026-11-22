using System.Net;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Resume addresses in two browsers (two clients with their own cookies): there are no resume links, so a resume token
/// in an address hands nothing to another browser, and the sharer keeps their response.
/// </summary>
public sealed partial class SurveyRespondingTests
{
    [Fact]
    public async Task ResumeToken_InTheAddress_HandsNothingToAnotherBrowser_AndTheOwnerKeepsTheResponse()
    {
        // Arrange: browser A starts a response on the public link and puts its own resume token in an address, as the
        // removed resume links did.
        var (surveyId, questionId, token) = await PublishAsync("Fixation check");
        var cancellationToken = TestContext.Current.CancellationToken;
        var (browserA, cookiesA) = CreateBrowser();
        var (browserB, _) = CreateBrowser();

        using (browserA)
        using (browserB)
        {
            var responsePath = await PostStartAsync(browserA, token);
            var responseId = responsePath.Substring(responsePath.LastIndexOf('/') + 1);
            var page = await browserA.GetStringAsync(responsePath, cancellationToken);
            var sharedLink = $"s/r/{responseId}?t={Uri.EscapeDataString(ResumeTokenOf(cookiesA, surveyId))}";

            // Act: browser B opens the address twice (the second time as two at once), then tries to save into it.
            var opened = await browserB.GetAsync(sharedLink, cancellationToken);
            var racing = await Task.WhenAll(browserB.GetAsync(sharedLink, cancellationToken), browserB.GetAsync(sharedLink, cancellationToken));
            var resumePost = await browserB.PostAsync($"s/r/{responseId}/resume", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = SurveysSiteFixture.Token(page),
                ["token"] = ResumeTokenOf(cookiesA, surveyId),
            }), cancellationToken);
            var saved = await browserB.PostAsync($"s/r/{responseId}/save", new MultipartFormDataContent
            {
                { new StringContent(SurveysSiteFixture.Token(page)), "__RequestVerificationToken" },
                { new StringContent(PageId(page)), "pageId" },
                { new StringContent("written by someone else"), $"q.{questionId}" },
            }, cancellationToken);
            var ownerReloads = await browserA.GetAsync(responsePath, cancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, opened.StatusCode);
            Assert.All(racing, result => Assert.Equal(HttpStatusCode.NotFound, result.StatusCode));
            Assert.Contains(resumePost.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.MethodNotAllowed });
            Assert.NotEqual(HttpStatusCode.OK, saved.StatusCode);
            Assert.NotEqual(HttpStatusCode.Redirect, saved.StatusCode);
            Assert.Equal(HttpStatusCode.OK, ownerReloads.StatusCode);
            Assert.False((await LatestResponseAsync(surveyId)).Answers.ContainsKey(questionId));
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
