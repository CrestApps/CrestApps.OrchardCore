using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// Resume tokens: a resume link's token is traded for a new one once, a personal invitation resumes its own response
/// from another browser, the start lock never holds a raw token, and resume cookies stop counting after sign-in or out.
/// </summary>
public sealed class SurveyResumeTokenTests
{
    [Fact]
    public async Task Rotate_WithTheCurrentToken_IssuesANewToken_AndTheOldOneStopsWorking()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(TwoPages());
        var start = await harness.StartAsync(survey);

        // Act
        var rotated = await harness.ResponsesAsync(responses => responses.RotateResumeTokenAsync(start.Response.ItemId, start.ResumeToken));
        var again = await harness.ResponsesAsync(responses => responses.RotateResumeTokenAsync(start.Response.ItemId, start.ResumeToken));
        var byOld = await harness.ResponsesAsync(responses => responses.FindForRespondentAsync(start.Response.ItemId, new SurveyRespondentIdentity { ResumeToken = start.ResumeToken }));
        var byNew = await harness.ResponsesAsync(responses => responses.FindForRespondentAsync(start.Response.ItemId, new SurveyRespondentIdentity { ResumeToken = rotated }));

        // Assert
        Assert.NotNull(start.ResumeToken);
        Assert.NotNull(rotated);
        Assert.NotEqual(start.ResumeToken, rotated);
        Assert.Null(again);
        Assert.Null(byOld);
        Assert.NotNull(byNew);
    }

    [Fact]
    public async Task Rotate_WithTheTokenOfAnotherResponse_IsRefused()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(TwoPages());
        var first = await harness.StartAsync(survey);
        var second = await harness.StartAsync(survey);

        // Act
        var crossed = await harness.ResponsesAsync(responses => responses.RotateResumeTokenAsync(first.Response.ItemId, second.ResumeToken));
        var forged = await harness.ResponsesAsync(responses => responses.RotateResumeTokenAsync(first.Response.ItemId, first.Response.ItemId + ".forged"));

        // Assert
        Assert.NotEqual(first.Response.ItemId, second.Response.ItemId);
        Assert.Null(crossed);
        Assert.Null(forged);
    }

    [Fact]
    public async Task Start_FromAnInvitation_ResumesItsResponseInAnotherBrowser_WithANewToken()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(TwoPages());
        const string invitationToken = "invitation-token-1";

        await harness.RunAsync(async services =>
        {
            var collector = await services.GetRequiredService<SurveyCollectorStore>().FindDefaultAsync(survey.ItemId);

            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(new SurveyInvitation
            {
                SurveyId = survey.ItemId,
                CollectorId = collector.ItemId,
                Email = "guest@example.com",
                TokenHash = SurveyRespondentKeys.Hash(invitationToken),
                Status = SurveyInvitationStatus.Sent,
            });

            return true;
        });

        // Act: a mail scanner opens and starts the link first, then the invitee opens it in their own browser.
        var scanner = await harness.StartAsync(survey, configure: request => request.InvitationToken = invitationToken);
        var invitee = await harness.StartAsync(survey, configure: request => request.InvitationToken = invitationToken);
        var scannerAfter = await harness.ResponsesAsync(responses => responses.FindForRespondentAsync(scanner.Response.ItemId, new SurveyRespondentIdentity { ResumeToken = scanner.ResumeToken }));

        // Assert
        Assert.True(scanner.Succeeded);
        Assert.True(invitee.Succeeded);
        Assert.True(invitee.Resumed);
        Assert.Equal(scanner.Response.ItemId, invitee.Response.ItemId);
        Assert.NotNull(invitee.ResumeToken);
        Assert.NotEqual(scanner.ResumeToken, invitee.ResumeToken);
        Assert.Null(scannerAfter);
    }

    [Fact]
    public void StartLockKey_HoldsTheHashOfTheResumeToken_NotTheToken()
    {
        // Arrange
        var token = SurveyResumeTokens.Issue("R1");

        // Act
        var key = SurveyResponseService.StartLockKey("s1", respondentKey: null, token);

        // Assert
        Assert.DoesNotContain(token, key, StringComparison.Ordinal);
        Assert.DoesNotContain(token.Substring(token.IndexOf('.') + 1), key, StringComparison.Ordinal);
        Assert.Equal("Surveys:Start:s1:t:" + SurveyResumeTokens.Hash(token), key);
        Assert.Equal("Surveys:Start:s1:u:u1", SurveyResponseService.StartLockKey("s1", "u:u1", token));
    }

    [Fact]
    public void Cookies_ParseOnlyTheCurrentGeneration()
    {
        // Arrange
        var value = SurveyResumeCookies.Format("R1.secret", "gen1");

        // Act and Assert
        Assert.Equal("R1.secret", SurveyResumeCookies.Parse(value, "gen1"));
        Assert.Null(SurveyResumeCookies.Parse(value, "gen2"));
        Assert.Null(SurveyResumeCookies.Parse(value, null));
        Assert.Null(SurveyResumeCookies.Parse("R1.secret", "gen1"));
    }

    [Fact]
    public void Cookies_WriteThenRead_OnTheTenantRespondentPath()
    {
        // Arrange
        var (cookies, context) = Cookies(new DefaultHttpContext());

        context.Request.PathBase = "/tenant1";

        // Act
        cookies.Write("s1", "R1.secret");

        // Assert
        var headers = context.Response.Headers.SetCookie.ToArray();

        Assert.Contains(headers, header => header.StartsWith(SurveyResumeCookies.GenerationCookie + "=", StringComparison.Ordinal) && header.Contains("path=/tenant1/s", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headers, header => header.StartsWith("sr_s1=", StringComparison.Ordinal) && header.Contains("httponly", StringComparison.OrdinalIgnoreCase) && header.Contains("path=/tenant1/s", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Cookies_AfterSignInOrOut_TheOldResumeCookieGivesNoToken()
    {
        // Arrange: the browser holds a resume cookie of generation "old".
        var request = new DefaultHttpContext();

        request.Request.Headers.Cookie = $"{SurveyResumeCookies.GenerationCookie}=old; sr_s1={Uri.EscapeDataString(SurveyResumeCookies.Format("R1.secret", "old"))}";

        var (cookies, context) = Cookies(request);
        var before = cookies.Read("s1");

        // Act
        cookies.ClearAll();
        var after = cookies.Read("s1");

        // Assert
        Assert.Equal("R1.secret", before);
        Assert.Null(after);

        var headers = context.Response.Headers.SetCookie.ToArray();

        Assert.Contains(headers, header => header.StartsWith("sr_s1=;", StringComparison.Ordinal));
        Assert.Contains(headers, header => header.StartsWith(SurveyResumeCookies.GenerationCookie + "=", StringComparison.Ordinal) && !header.StartsWith(SurveyResumeCookies.GenerationCookie + "=old", StringComparison.Ordinal));
    }

    private static (SurveyResumeCookies Cookies, HttpContext Context) Cookies(HttpContext context)
        => (new SurveyResumeCookies(new HttpContextAccessor { HttpContext = context }), context);

    private static SurveyDefinition TwoPages()
        => RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1", required: true)),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")));
}
