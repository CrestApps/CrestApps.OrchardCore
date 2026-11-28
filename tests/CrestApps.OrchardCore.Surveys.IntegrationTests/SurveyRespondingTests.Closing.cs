using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Over HTTP: once the closing date passes nothing is reopened (P1-H1); a page the runner script posts comes back as
/// where to go, with its errors carried over, so the script can keep the answers and retry on 429 (P1-M4); an abandoned
/// response doesn't thank the respondent (P1-L8); and a kiosk response page goes back to the start when left alone
/// (P1-L7).
/// </summary>
public sealed partial class SurveyRespondingTests
{
    [Fact]
    public async Task ClosingDatePassed_TheEndingOffersNoChange_AndReopenIsRefused()
    {
        // Arrange: changes after finishing are allowed.
        var (surveyId, questionId, token) = await PublishAsync("Closing date check", definition => definition.Navigation.AllowEditAfterSubmit = true);
        var (browser, _) = CreateBrowser();
        using var scope = browser;
        var cancellationToken = TestContext.Current.CancellationToken;
        var responsePath = await PostStartAsync(browser, token);
        var page = await browser.GetStringAsync(responsePath, cancellationToken);

        await browser.PostAsync(responsePath, new MultipartFormDataContent
        {
            { new StringContent(SurveysSiteFixture.Token(page)), "__RequestVerificationToken" },
            { new StringContent(PageId(page)), "pageId" },
            { new StringContent("finish"), "direction" },
            { new StringContent("First answer"), $"q.{questionId}" },
        }, cancellationToken);
        var openEnding = await browser.GetStringAsync(responsePath + "/done", cancellationToken);

        // Act: the closing date passes, though nobody closes the survey.
        await SetClosesAsync(surveyId, DateTime.UtcNow.AddMinutes(-1));
        var closedEnding = await browser.GetStringAsync(responsePath + "/done", cancellationToken);
        var reopen = await browser.PostAsync(responsePath + "/reopen", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(openEnding),
        }), cancellationToken);
        var stored = await ResponseOfPathAsync(responsePath);

        // Assert
        Assert.Contains("Change Your Answers", openEnding, StringComparison.Ordinal);
        Assert.DoesNotContain("Change Your Answers", closedEnding, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, reopen.StatusCode);
        Assert.Equal(SurveyResponseStatus.Completed, stored.Status);
        Assert.Equal("First answer", stored.Answers[questionId].Values.Single());
    }

    [Fact]
    public async Task ScriptPost_ARefusedPage_SaysWhereToGo_AndThatPageShowsTheErrors()
    {
        // Arrange
        var (_, _, token) = await PublishAsync("Script post check");
        var (browser, _) = CreateBrowser();
        using var scope = browser;
        var cancellationToken = TestContext.Current.CancellationToken;
        var responsePath = await PostStartAsync(browser, token);
        var page = await browser.GetStringAsync(responsePath, cancellationToken);

        // Act: the required answer is left out.
        using var request = new HttpRequestMessage(HttpMethod.Post, responsePath)
        {
            Content = new MultipartFormDataContent
            {
                { new StringContent(SurveysSiteFixture.Token(page)), "__RequestVerificationToken" },
                { new StringContent(PageId(page)), "pageId" },
                { new StringContent("finish"), "direction" },
            },
        };
        request.Headers.Add("X-Survey-Fetch", "1");
        var posted = await browser.SendAsync(request, cancellationToken);
        var json = JsonDocument.Parse(await posted.Content.ReadAsStringAsync(cancellationToken));
        var redirect = json.RootElement.GetProperty("redirect").GetString();
        var shown = await browser.GetStringAsync(redirect, cancellationToken);
        var again = await browser.GetStringAsync(responsePath, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        Assert.StartsWith("/s/r/", redirect, StringComparison.Ordinal);
        Assert.Contains("is-invalid", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("is-invalid", again, StringComparison.Ordinal);
        Assert.Contains("data-retry-message", shown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AbandonedResponse_TheEndingSaysItWasNotSubmitted()
    {
        // Arrange
        var (_, _, token) = await PublishAsync("Abandoned check");
        var (browser, _) = CreateBrowser();
        using var scope = browser;
        var cancellationToken = TestContext.Current.CancellationToken;
        var responsePath = await PostStartAsync(browser, token);

        await _fixture.InTenantAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyResponseStore>();
            var response = await store.FindByIdAsync(responsePath.Substring(responsePath.LastIndexOf('/') + 1));

            response.Status = SurveyResponseStatus.Abandoned;
            await store.UpdateAsync(response);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync();
        });

        // Act
        var ending = await browser.GetStringAsync(responsePath + "/done", cancellationToken);

        // Assert
        Assert.Contains("was not submitted", ending, StringComparison.Ordinal);
        Assert.DoesNotContain("Thank you!", ending, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KioskResponsePage_GoesBackToTheStart_WhenLeftAlone()
    {
        // Arrange
        var (surveyId, _, token) = await PublishAsync("Kiosk idle check");

        await MakeKioskAsync(surveyId);

        var (device, _) = CreateBrowser();
        using var scope = device;

        // Act
        var responsePath = await PostStartAsync(device, token);
        var page = await device.GetStringAsync(responsePath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains($"data-survey-kiosk-idle-url=\"/s/{token}\"", page, StringComparison.Ordinal);
        Assert.Contains("data-survey-kiosk-idle-seconds=\"180\"", page, StringComparison.Ordinal);
    }

    private Task SetClosesAsync(string surveyId, DateTime closesUtc)
        => _fixture.InTenantAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyStore>();
            var survey = await store.FindByIdAsync(surveyId);

            survey.Access ??= new SurveyAccessSettings();
            survey.Access.ClosesUtc = closesUtc;

            await store.UpdateAsync(survey);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync();
        });
}
