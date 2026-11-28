using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// Resume tokens: no service call trades a presented token for a new one (there are no resume links), a personal
/// invitation resumes its own response from another browser, the start lock never holds a raw token, resume cookies stop
/// counting after sign-in or out, and the generation cookie sits on the tenant root so a page outside /s that writes a
/// resume cookie keeps the browser's other responses.
/// </summary>
public sealed class SurveyResumeTokenTests
{
    [Fact]
    public void ResponseService_HasNoWayToTradeAPresentedResumeToken()
    {
        // Arrange: a resume link traded the token on it for a new one, which let whoever opened a shared link take the
        // response (and its answers) over. Resume links are gone, and so is the call that traded the token.
        var methods = typeof(ISurveyResponseService).GetMethods().Select(method => method.Name);

        // Act and Assert
        Assert.DoesNotContain(methods, name => name.Contains("Rotate", StringComparison.Ordinal));
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

        // The generation sits on the tenant root, so pages outside /s (a course, admin Preview) see it.
        Assert.Contains(headers, header => header.StartsWith(Generation + "=", StringComparison.Ordinal) && header.Contains("path=/tenant1;", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(headers, header => header.StartsWith(Generation + "=", StringComparison.Ordinal) && header.Contains("path=/tenant1/s", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headers, header => header.StartsWith("sr_s1=", StringComparison.Ordinal) && header.Contains("httponly", StringComparison.OrdinalIgnoreCase) && header.Contains("path=/tenant1/s", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Cookies_GenerationWithoutPathBase_IsOnTheSiteRoot()
    {
        // Arrange
        var (cookies, context) = Cookies(new DefaultHttpContext());

        // Act
        cookies.Write("s1", "R1.secret");

        // Assert
        var generation = Assert.Single(context.Response.Headers.SetCookie.ToArray(), header => header.StartsWith(Generation + "=", StringComparison.Ordinal));

        Assert.Contains("path=/;", generation + ";", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cookies_WrittenOutsideTheRespondentPath_JoinTheCurrentGeneration_AndKeepTheOtherResponses()
    {
        // Arrange: the browser holds survey s1's resume cookie; a course page (outside /s) sees only the generation
        // cookie, which sits on the tenant root, and writes survey s2's resume cookie.
        var coursePage = new DefaultHttpContext();

        coursePage.Request.Path = "/learning/surveys/item1/start";
        coursePage.Request.Headers.Cookie = $"{Generation}=current";

        var (cookies, context) = Cookies(coursePage);

        // Act
        cookies.Write("s2", "R2.secret");

        // Assert: no new generation, so s1's cookie (generation "current") still counts on the next respondent page.
        var headers = context.Response.Headers.SetCookie.ToArray();

        Assert.DoesNotContain(headers, header => header.StartsWith(Generation + "=", StringComparison.Ordinal));
        Assert.Contains(headers, header => header.StartsWith("sr_s2=" + Uri.EscapeDataString(SurveyResumeCookies.Format("R2.secret", "current")), StringComparison.Ordinal));
    }

    [Fact]
    public void Cookies_AfterSignInOrOut_TheOldResumeCookieGivesNoToken()
    {
        // Arrange: the browser holds a resume cookie of generation "old".
        var request = new DefaultHttpContext();

        request.Request.Headers.Cookie = $"{Generation}=old; sr_s1={Uri.EscapeDataString(SurveyResumeCookies.Format("R1.secret", "old"))}";

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
        Assert.Contains(headers, header => header.StartsWith(Generation + "=", StringComparison.Ordinal) && !header.StartsWith(Generation + "=old", StringComparison.Ordinal));
    }

    [Fact]
    public void GenerationCookieName_IsUniquePerTenant()
    {
        // Arrange: a tenant without a prefix writes its generation cookie on "/", which requests to "/t1/..." carry too.

        // Act
        var root = SurveyResumeCookies.GenerationCookieName("Default");
        var prefixed = SurveyResumeCookies.GenerationCookieName("t1");

        // Assert
        Assert.NotEqual(root, prefixed);
        Assert.StartsWith(SurveyResumeCookies.GenerationCookiePrefix, root, StringComparison.Ordinal);
        Assert.Equal(SurveyResumeCookies.GenerationCookiePrefix.Length + 8, root.Length);
        Assert.Equal(root, SurveyResumeCookies.GenerationCookieName("Default"));
    }

    [Fact]
    public void Cookies_OfAPrefixedTenant_IgnoreTheRootTenantsGenerationCookie()
    {
        // Arrange: a request to tenant t1 carries the root tenant's generation cookie (path "/") and t1's resume cookie,
        // written under t1's own generation.
        var request = new DefaultHttpContext();

        request.Request.PathBase = "/t1";
        request.Request.Headers.Cookie = $"{SurveyResumeCookies.GenerationCookieName("Default")}=rootgen; {SurveyResumeCookies.GenerationCookieName("t1")}=t1gen; sr_s1={Uri.EscapeDataString(SurveyResumeCookies.Format("R1.secret", "t1gen"))}";

        var cookies = new SurveyResumeCookies(new HttpContextAccessor { HttpContext = request }, new ShellSettings { Name = "t1" });

        // Act
        var token = cookies.Read("s1");
        cookies.ClearAll();

        // Assert: t1 reads its own generation, and signing in or out on t1 never touches the root tenant's cookie.
        Assert.Equal("R1.secret", token);
        Assert.DoesNotContain(request.Response.Headers.SetCookie.ToArray(), header => header.StartsWith(SurveyResumeCookies.GenerationCookieName("Default") + "=", StringComparison.Ordinal));
    }

    [Fact]
    public void Cookies_AfterCompletion_LiveOneDay_AndStaleOnesAreRemovedOnTheNextWrite()
    {
        // Arrange: the browser holds a current resume cookie for s1 and one of an older generation for s2.
        var request = new DefaultHttpContext();

        request.Request.Headers.Cookie = $"{Generation}=current; sr_s1={Uri.EscapeDataString(SurveyResumeCookies.Format("R1.secret", "current"))}; sr_s2={Uri.EscapeDataString(SurveyResumeCookies.Format("R2.secret", "old"))}";

        var (cookies, context) = Cookies(request);

        // Act
        cookies.ShortenAfterCompletion("s1");
        cookies.Write("s3", "R3.secret");

        // Assert
        var headers = context.Response.Headers.SetCookie.ToArray();

        Assert.Contains(headers, header => header.StartsWith("sr_s1=", StringComparison.Ordinal) && header.Contains("max-age=86400", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headers, header => header.StartsWith("sr_s2=;", StringComparison.Ordinal));
        Assert.DoesNotContain(headers, header => header.StartsWith("sr_s1=;", StringComparison.Ordinal));
    }

    private static string Generation
        => SurveyResumeCookies.GenerationCookieName("Default");

    private static (SurveyResumeCookies Cookies, HttpContext Context) Cookies(HttpContext context)
        => (new SurveyResumeCookies(new HttpContextAccessor { HttpContext = context }, new ShellSettings { Name = "Default" }), context);

    private static SurveyDefinition TwoPages()
        => RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1", required: true)),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q2")));
}
