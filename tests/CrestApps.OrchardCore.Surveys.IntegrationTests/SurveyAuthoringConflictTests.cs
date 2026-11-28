using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// The designer refuses a save over a draft another author changed since the page was loaded, and the Surveys list
/// refuses to delete a survey another feature owns.
/// </summary>
[Collection(SurveysSiteCollection.Name)]
public sealed partial class SurveyAuthoringConflictTests
{
    private readonly SurveysSiteFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAuthoringConflictTests"/> class.
    /// </summary>
    /// <param name="fixture">The site.</param>
    public SurveyAuthoringConflictTests(SurveysSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DesignerCommand_AfterAnotherAuthorChangedTheDraft_IsRefusedWith409()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var surveyId = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ISurveyManager>().CreateSurveyAsync(new SurveyCreateRequest { Title = "Conflict " + Guid.NewGuid().ToString("N")[..6] }, cancellationToken)).ItemId);
        using var client = await _fixture.CreateClientAsync("admin");
        var page = await client.GetStringAsync($"Admin/surveys/{surveyId}/design", cancellationToken);
        var token = SurveysSiteFixture.Token(page);
        var seen = DraftVersionPattern().Match(page).Groups["version"].Value;
        Assert.False(string.IsNullOrEmpty(seen));

        // Another author saves the draft a few seconds later.
        await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<ISurveyManager>();
            var survey = await manager.FindByIdAsync(surveyId, cancellationToken);
            var draft = survey.Draft;
            draft.Pages[0].Title = "Their change";
            await manager.SaveDraftAsync(survey, draft, cancellationToken: cancellationToken);
            survey.DraftModifiedUtc = survey.DraftModifiedUtc.Value.AddSeconds(5);
            await manager.UpdateAsync(survey, null, cancellationToken);
        });

        // Act
        var stale = await SendAsync(client, surveyId, token, seen, cancellationToken);
        var current = await SendAsync(client, surveyId, token, null, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var pages = await _fixture.InTenantAsync(async services => (await services.GetRequiredService<ISurveyManager>().FindByIdAsync(surveyId, cancellationToken)).Draft.Pages);
        Assert.Equal("Their change", pages[0].Title);
    }

    [Fact]
    public async Task Delete_OfASurveyAnotherFeatureOwns_IsRefused()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var surveyId = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ISurveyManager>().CreateSurveyAsync(new SurveyCreateRequest
            {
                Title = "Owned " + Guid.NewGuid().ToString("N")[..6],
                Owner = new RecordRef("TestOwner", "owner-1"),
            }, cancellationToken)).ItemId);
        using var client = await _fixture.CreateClientAsync("admin");
        var token = SurveysSiteFixture.Token(await client.GetStringAsync($"Admin/surveys/{surveyId}/design", cancellationToken));

        // Act
        var response = await client.PostAsync($"Admin/surveys/{surveyId}/delete", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }), cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(await _fixture.InTenantAsync(async services => await services.GetRequiredService<ISurveyManager>().FindByIdAsync(surveyId, cancellationToken)));
    }

    [Fact]
    public async Task DiscardDraft_WhenThePublishedVersionIsMissing_ReportsTheFailureAndKeepsTheUnpublishedChanges()
    {
        // Arrange: R12-L12, the survey points at a published version that can't be found any more.
        var cancellationToken = TestContext.Current.CancellationToken;
        var surveyId = await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<ISurveyManager>();
            var survey = await manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Discard " + Guid.NewGuid().ToString("N")[..6] }, cancellationToken);

            survey.PublishedVersionId = "missing-version";
            survey.HasUnpublishedChanges = true;
            await manager.UpdateAsync(survey, null, cancellationToken);

            return survey.ItemId;
        });
        using var client = await _fixture.CreateClientAsync("admin");
        var token = SurveysSiteFixture.Token(await client.GetStringAsync($"Admin/surveys/{surveyId}/design", cancellationToken));

        // Act
        var discarded = await client.PostAsync($"Admin/surveys/{surveyId}/draft/discard", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }), cancellationToken);
        var page = await client.GetStringAsync(discarded.Headers.Location, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, discarded.StatusCode);
        Assert.DoesNotContain("The draft has been replaced with the published version.", page, StringComparison.Ordinal);
        Assert.Contains("The published version could not be found", page, StringComparison.Ordinal);
        Assert.True(await _fixture.InTenantAsync(async services => (await services.GetRequiredService<ISurveyManager>().FindByIdAsync(surveyId, cancellationToken)).HasUnpublishedChanges));
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string surveyId, string token, string version, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"Admin/surveys/{surveyId}/draft/commands")
        {
            Content = new StringContent("{\"command\":\"addPage\"}", Encoding.UTF8, "application/json"),
        };

        request.Headers.Add("RequestVerificationToken", token);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        if (version is not null)
        {
            request.Headers.Add("X-Survey-Draft-Version", version);
        }

        return client.SendAsync(request, cancellationToken);
    }

    [GeneratedRegex("name=\"draftVersion\" value=\"(?<version>\\d+)\"")]
    private static partial Regex DraftVersionPattern();
}
