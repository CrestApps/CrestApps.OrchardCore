using System.Net;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// The unsubscribe page with a link it can't match (R4-M4) and the life of a guest's resume cookie after the ending
/// (R4-L1).
/// </summary>
public sealed partial class SurveyRespondingTests
{
    [Fact]
    public async Task Unsubscribe_WithAnUnknownToken_ShowsTheFriendlyPage_OnGetAndPost()
    {
        // Arrange
        using var http = await _fixture.CreateClientAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act: a link whose invitation was deleted, and a mail app's one-click unsubscribe post without a form token.
        var opened = await http.GetAsync("s/i/not-a-real-token/unsubscribe", cancellationToken);
        var posted = await http.PostAsync("s/i/not-a-real-token/unsubscribe", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["List-Unsubscribe"] = "One-Click",
        }), cancellationToken);

        // Assert
        foreach (var result in new[] { opened, posted })
        {
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
            Assert.Contains("Survey not found", await result.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(false, false, 86400)]
    [InlineData(true, false, 2592000)]
    [InlineData(false, true, 2592000)]
    public async Task Done_KeepsTheResumeCookieFullLife_OnlyWhileTheGuestMayReopenOrReviewLater(bool editAfterSubmit, bool reviewLater, int expectedMaxAge)
    {
        // Arrange
        var (surveyId, questionId, token) = await PublishWithSettingsAsync(
            "Cookie life " + Guid.NewGuid().ToString("N")[..8],
            definition =>
            {
                definition.Navigation.AllowEditAfterSubmit = editAfterSubmit;
                definition.Review.LaterWhileOpen = reviewLater ? ReviewShows.Responses : ReviewShows.None;

                // The review after close shows the answers by default, which keeps the cookie too while the survey is open.
                definition.Review.AfterClose = ReviewShows.None;
            });

        using var http = await _fixture.CreateClientAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var landing = await http.GetStringAsync($"s/{token}", cancellationToken);
        var started = await http.PostAsync($"s/{token}/start", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(landing),
        }), cancellationToken);
        var responsePath = started.Headers.Location?.OriginalString;
        var page = await http.GetStringAsync(responsePath, cancellationToken);
        var submitted = await http.PostAsync(responsePath, new MultipartFormDataContent
        {
            { new StringContent(SurveysSiteFixture.Token(page)), "__RequestVerificationToken" },
            { new StringContent(PageId(page)), "pageId" },
            { new StringContent("finish"), "direction" },
            { new StringContent("Fine"), $"q.{questionId}" },
        }, cancellationToken);

        // Act
        var done = await http.GetAsync(submitted.Headers.Location?.OriginalString, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);

        var cookie = Assert.Single(done.Headers.TryGetValues("Set-Cookie", out var values) ? values : [], value => value.StartsWith("sr_" + surveyId + "=", StringComparison.Ordinal));

        Assert.Contains($"max-age={expectedMaxAge}", cookie, StringComparison.OrdinalIgnoreCase);
    }

    private Task<(string SurveyId, string QuestionId, string Token)> PublishWithSettingsAsync(string title, Action<SurveyDefinition> configure)
        => _fixture.InTenantAsync(async services =>
        {
            var definition = new SurveyDefinition
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
                            new SurveyQuestion { Id = "q1", Name = "comment", ControlType = SurveyControlNames.ShortText, Title = "How was it?", Required = true },
                        ],
                    },
                ],
                Endings = [new SurveyEnding { Id = "e1", Name = "thanks", Title = "Thanks", Message = "Thank you!", IsDefault = true }],
            };

            configure(definition);

            var manager = services.GetRequiredService<ISurveyManager>();
            var survey = await manager.CreateSurveyAsync(new SurveyCreateRequest { Title = title, Definition = definition });
            var published = await services.GetRequiredService<ISurveyPublisher>().PublishAsync(survey.ItemId, "First version", "admin");

            Assert.True(published.Succeeded, string.Join(", ", published.Messages.Where(message => message.IsError).Select(message => message.Message)));

            var reloaded = await manager.FindByIdAsync(survey.ItemId);
            var collector = await services.GetRequiredService<SurveyCollectorStore>().FindDefaultAsync(survey.ItemId);

            return (survey.ItemId, reloaded.Draft.Pages[0].Questions[0].Id, collector.Token);
        });
}
