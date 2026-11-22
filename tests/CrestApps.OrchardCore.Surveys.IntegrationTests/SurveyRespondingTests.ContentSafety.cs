using System.Net;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Author HTML is sanitized on the public runner, and a respondent's answer piped into the ending comes out as text,
/// never as Liquid.
/// </summary>
public sealed partial class SurveyRespondingTests
{
    private const string TitlePayload = "Your name? <img src=x onerror=\"document.title='XSS-TITLE'\">";
    private const string DescriptionPayload = "<p>Be honest</p><script>document.title='XSS-DESCRIPTION'</script>";
    private const string LiquidAnswer = "[{{ 7 | plus: 6 }}] [{{ Site.SiteName }}] [{{ Request.Host }}]";

    [Fact]
    public async Task PublicRunner_TitleAndDescriptionPayloads_AreRenderedWithoutScript()
    {
        // Arrange
        var (_, _, token) = await PublishUnsafeAsync("Script check");
        using var http = await _fixture.CreateClientAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var landing = await http.GetStringAsync($"s/{token}", cancellationToken);
        var started = await http.PostAsync($"s/{token}/start", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(landing),
        }), cancellationToken);

        var page = await http.GetStringAsync(started.Headers.Location?.OriginalString, cancellationToken);

        // Assert
        Assert.Contains("Your name?", page, StringComparison.Ordinal);
        Assert.Contains("<p>Be honest</p>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("onerror", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("XSS-TITLE", page, StringComparison.Ordinal);
        Assert.DoesNotContain("XSS-DESCRIPTION", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ending_PipedAnswerWithLiquid_ComesOutLiterally()
    {
        // Arrange
        var (_, questionId, token) = await PublishUnsafeAsync("Liquid check");
        using var http = await _fixture.CreateClientAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var landing = await http.GetStringAsync($"s/{token}", cancellationToken);
        var started = await http.PostAsync($"s/{token}/start", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(landing),
        }), cancellationToken);

        var responsePath = started.Headers.Location?.OriginalString;
        var page = await http.GetStringAsync(responsePath, cancellationToken);

        // Act
        var submitted = await http.PostAsync(responsePath, new MultipartFormDataContent
        {
            { new StringContent(SurveysSiteFixture.Token(page)), "__RequestVerificationToken" },
            { new StringContent(PageId(page)), "pageId" },
            { new StringContent("finish"), "direction" },
            { new StringContent(LiquidAnswer), $"q.{questionId}" },
        }, cancellationToken);

        var done = await http.GetStringAsync(submitted.Headers.Location?.OriginalString, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);
        Assert.Contains("You said:", done, StringComparison.Ordinal);
        Assert.Contains("7 | plus: 6", done, StringComparison.Ordinal);
        Assert.Contains("Site.SiteName", done, StringComparison.Ordinal);
        Assert.DoesNotContain("[13]", done, StringComparison.Ordinal);
    }

    private Task<(string SurveyId, string QuestionId, string Token)> PublishUnsafeAsync(string title)
        => _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<ISurveyManager>();
            var survey = await manager.CreateSurveyAsync(new SurveyCreateRequest
            {
                Title = title,
                Definition = new SurveyDefinition
                {
                    Title = title,
                    Pages =
                    [
                        new SurveyPage
                        {
                            Id = "p1",
                            Name = "page_1",
                            Title = "Page 1",
                            Questions =
                            [
                                new SurveyQuestion { Id = "q1", Name = "comment", ControlType = SurveyControlNames.ShortText, Title = TitlePayload, Description = DescriptionPayload },
                            ],
                        },
                    ],
                    Endings = [new SurveyEnding { Id = "e1", Name = "thanks", Title = "Thanks", Message = "<p>You said: {comment}</p>", IsDefault = true }],
                },
            });

            var published = await services.GetRequiredService<ISurveyPublisher>().PublishAsync(survey.ItemId, "First version", "admin");

            Assert.True(published.Succeeded, string.Join(", ", published.Messages.Where(message => message.IsError).Select(message => message.Message)));

            var reloaded = await manager.FindByIdAsync(survey.ItemId);
            var collector = await services.GetRequiredService<SurveyCollectorStore>().FindDefaultAsync(survey.ItemId);

            return (survey.ItemId, reloaded.Draft.Pages[0].Questions[0].Id, collector.Token);
        });
}
