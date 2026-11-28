using System.Net;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Round 12 L3 and L18: a link's password (an access code) locks a respondent out after five wrong tries, the CAPTCHA is
/// checked before the password, so a password can't be guessed without solving it, and a survey's statement (an honor
/// statement) must be accepted before a response starts.
/// </summary>
public sealed partial class SurveyRespondingTests
{
    private const string AccessCode = "Open-sesame-42";

    [Fact]
    public async Task R12L18_AWrongPassword_IsRefused_AndFiveWrongTriesLockTheRespondentOut()
    {
        // Arrange
        var (surveyId, _, token) = await PublishAsync("Access code lockout check");

        await SetPasswordAsync(surveyId, AccessCode);

        var (browser, _) = CreateBrowser();
        using var scope = browser;

        // Act: five wrong codes, then the right one.
        var wrong = new List<string>();

        for (var tries = 0; tries < 5; tries++)
        {
            wrong.Add(await (await PostStartWithAsync(browser, token, AccessCode + "x", acceptStatement: false)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        var right = await PostStartWithAsync(browser, token, AccessCode, acceptStatement: false);
        var rightPage = await right.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var count = await ResponseCountAsync(surveyId);

        // Assert
        Assert.All(wrong, page => Assert.Contains("The password is not correct.", page, StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Contains("Too many wrong passwords", rightPage, StringComparison.Ordinal);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task R12L18_TheRightPassword_StartsAResponse()
    {
        // Arrange
        var (surveyId, _, token) = await PublishAsync("Access code right check");

        await SetPasswordAsync(surveyId, AccessCode);

        var (browser, _) = CreateBrowser();
        using var scope = browser;

        // Act
        var started = await PostStartWithAsync(browser, token, AccessCode, acceptStatement: false);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
        Assert.StartsWith("/s/r/", started.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task R12L3_ThePassword_IsNotCheckedBeforeTheCaptcha()
    {
        // Arrange: a survey that asks visitors for a CAPTCHA and a password.
        var (surveyId, _, token) = await PublishAsync("Captcha before password check");

        await SetPasswordAsync(surveyId, AccessCode);
        await _fixture.InTenantAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyStore>();
            var survey = await store.FindByIdAsync(surveyId);

            survey.Access.RequireCaptcha = true;
            await store.UpdateAsync(survey);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync();
        });

        var (browser, _) = CreateBrowser();
        using var scope = browser;

        // Act: a guess without a CAPTCHA.
        var guess = await PostStartWithAsync(browser, token, AccessCode + "x", acceptStatement: false);
        var page = await guess.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert: the CAPTCHA refuses the start; the guess is never checked, so it tells nothing about the password.
        Assert.DoesNotContain("The password is not correct.", page, StringComparison.Ordinal);
        Assert.Equal(0, await ResponseCountAsync(surveyId));
    }

    [Fact]
    public async Task R12L18_AStartThatDoesNotAcceptTheStatement_IsRefused()
    {
        // Arrange
        var (surveyId, _, token) = await PublishAsync("Statement refused check");

        await SetStatementAsync(surveyId, "I will answer on my own, without help.");

        var (browser, _) = CreateBrowser();
        using var scope = browser;

        // Act
        var landing = await browser.GetStringAsync($"s/{token}", TestContext.Current.CancellationToken);
        var refused = await PostStartWithAsync(browser, token, password: null, acceptStatement: false);
        var page = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("I will answer on my own, without help.", landing, StringComparison.Ordinal);
        Assert.Contains("name=\"acceptStatement\"", landing, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("Accept the statement to start.", page, StringComparison.Ordinal);
        Assert.Equal(0, await ResponseCountAsync(surveyId));
    }

    [Fact]
    public async Task R12L18_AStartThatAcceptsTheStatement_StartsAndRecordsTheAcceptance()
    {
        // Arrange
        var (surveyId, _, token) = await PublishAsync("Statement accepted check");

        await SetStatementAsync(surveyId, "I will answer on my own, without help.");

        using var respondent = await _fixture.CreateClientAsync(SurveysSiteFixture.RespondentUserName);

        // Act
        var started = await PostStartWithAsync(respondent, token, password: null, acceptStatement: true);
        var response = await LatestResponseAsync(surveyId);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
        Assert.StartsWith("/s/r/", started.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.NotNull(response.StatementAcceptedUtc);
    }

    private static async Task<HttpResponseMessage> PostStartWithAsync(HttpClient browser, string collectorToken, string password, bool acceptStatement)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var landing = await browser.GetStringAsync($"s/{collectorToken}", cancellationToken);
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(landing),
        };

        if (password is not null)
        {
            form["password"] = password;
        }

        if (acceptStatement)
        {
            form["acceptStatement"] = "true";
        }

        return await browser.PostAsync($"s/{collectorToken}/start", new FormUrlEncodedContent(form), cancellationToken);
    }

    private Task SetPasswordAsync(string surveyId, string password)
        => _fixture.InTenantAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyCollectorStore>();
            var collector = await store.FindDefaultAsync(surveyId);

            SurveyCollectorManager.SetPassword(collector, password);
            await store.UpdateAsync(collector);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync();
        });

    private Task SetStatementAsync(string surveyId, string statement)
        => _fixture.InTenantAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyStore>();
            var survey = await store.FindByIdAsync(surveyId);

            survey.Access ??= new SurveyAccessSettings();
            survey.Access.Statement = statement;
            await store.UpdateAsync(survey);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync();
        });

    private Task<int> ResponseCountAsync(string surveyId)
        => _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ISurveyResponseQueries>().ListAsync(new SurveyResponseQuery { SurveyIds = [surveyId], Take = 1 })).Count);
}
