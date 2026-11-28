using System.Net;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// A survey is published and answered over HTTP on its public link and on a personal invitation link, and the API
/// refuses callers that are not signed in.
/// </summary>
[Collection(SurveysSiteCollection.Name)]
public sealed partial class SurveyRespondingTests
{
    private readonly SurveysSiteFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyRespondingTests"/> class.
    /// </summary>
    /// <param name="fixture">The site.</param>
    public SurveyRespondingTests(SurveysSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PublicLink_AnonymousRespondent_CompletesTheSurvey()
    {
        // Arrange
        var (surveyId, questionId, token) = await PublishAsync("Lunch feedback");
        using var http = await _fixture.CreateClientAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
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
            { new StringContent("The soup was great"), $"q.{questionId}" },
        }, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);
        Assert.EndsWith("/done", submitted.Headers.Location?.OriginalString, StringComparison.Ordinal);

        var response = await LatestResponseAsync(surveyId);

        Assert.Equal(SurveyResponseStatus.Completed, response.Status);
        Assert.Equal("The soup was great", response.Answers[questionId].Values.Single());
        Assert.Null(response.UserId);
    }

    [Fact]
    public async Task InvitationLink_OpensAResponseForTheInvitation()
    {
        // Arrange
        var (surveyId, _, _) = await PublishAsync("Onboarding check-in");
        var invitation = await _fixture.InTenantAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest
        {
            SurveyId = surveyId,
            Email = "guest@example.com",
        }));

        using var http = await _fixture.CreateClientAsync();
        var path = invitation.Link.Substring(invitation.Link.IndexOf("s/i/", StringComparison.Ordinal));

        // Act: opening the link shows a page that posts the start.
        var page = await http.GetStringAsync(path, TestContext.Current.CancellationToken);
        var opened = await http.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(page),
        }), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(invitation.Succeeded);
        Assert.Equal(HttpStatusCode.Redirect, opened.StatusCode);
        Assert.StartsWith("/s/r/", opened.Headers.Location?.OriginalString, StringComparison.Ordinal);

        var stored = await _fixture.InTenantAsync(services => services.GetRequiredService<SurveyInvitationStore>().FindByIdAsync(invitation.Invitation.ItemId).AsTask());

        Assert.Equal(SurveyInvitationStatus.Started, stored.Status);
    }

    [Fact]
    public async Task Api_WithoutSignIn_IsRefused()
    {
        // Arrange
        using var http = await _fixture.CreateClientAsync();

        // Act
        var result = await http.GetAsync("api/surveys", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    private Task<(string SurveyId, string QuestionId, string Token)> PublishAsync(string title, Action<SurveyDefinition> configure = null)
        => _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<ISurveyManager>();
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
                            new SurveyQuestion { Id = "q1", Name = "comment", ControlType = SurveyControlNames.ShortText, Title = "What did you think?", Required = true },
                        ],
                    },
                ],
                Endings = [new SurveyEnding { Id = "e1", Name = "thanks", Title = "Thanks", Message = "Thank you!", IsDefault = true }],
            };

            configure?.Invoke(definition);

            var survey = await manager.CreateSurveyAsync(new SurveyCreateRequest
            {
                Title = title,
                Definition = definition,
            });

            var published = await services.GetRequiredService<ISurveyPublisher>().PublishAsync(survey.ItemId, "First version", "admin");

            Assert.True(published.Succeeded, string.Join(", ", published.Messages.Where(message => message.IsError).Select(message => message.Message)));

            var reloaded = await manager.FindByIdAsync(survey.ItemId);
            var collector = await services.GetRequiredService<SurveyCollectorStore>().FindDefaultAsync(survey.ItemId);

            return (survey.ItemId, reloaded.Draft.Pages[0].Questions[0].Id, collector.Token);
        });

    private Task<SurveyResponse> LatestResponseAsync(string surveyId)
        => _fixture.InTenantAsync(async services =>
        {
            var (_, items) = await services.GetRequiredService<ISurveyResponseQueries>().ListAsync(new SurveyResponseQuery { SurveyIds = [surveyId], Take = 1 });

            return items.Single();
        });

    private static string PageId(string html)
        => PageIdPattern().Match(html) is { Success: true } match ? match.Groups["id"].Value : throw new InvalidOperationException("The page has no page id.");

    [GeneratedRegex("name=\"pageId\" value=\"(?<id>[^\"]+)\"")]
    private static partial Regex PageIdPattern();
}
