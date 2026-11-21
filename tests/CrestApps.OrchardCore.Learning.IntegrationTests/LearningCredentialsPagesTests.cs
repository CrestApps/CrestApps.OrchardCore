using System.Net;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Settings;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The pages of digital credentials: My credentials (the learner's own awards, the downloads of the credential and the
/// signed token, the choice to show their name), and the admin list, the award by hand and the revocation.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningCredentialsPagesTests
{
    private readonly LearningSiteFixture _fixture;

    public LearningCredentialsPagesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyCredentials_ListsTheLearnersAwards_AndDownloadsOnlyTheirOwn()
    {
        // Arrange
        var (award, definitionId) = await AwardAsync("credpage1");
        var other = (await AwardAsync("credpage2", definitionId)).Award;
        using var http = await _fixture.CreateClientAsync("credpage1");

        // Act
        var page = await http.GetAsync("learning/credentials", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var json = await http.GetAsync($"learning/credentials/mine/{award.ItemId}/json", TestContext.Current.CancellationToken);
        var jwt = await http.GetAsync($"learning/credentials/mine/{award.ItemId}/jwt", TestContext.Current.CancellationToken);
        var someoneElses = await http.GetAsync($"learning/credentials/mine/{other.ItemId}/json", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(page.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("Badge of credpage1", html, StringComparison.Ordinal);
        Assert.Contains($"learning/credentials/verify/{award.PublicId}", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, json.StatusCode);
        Assert.Equal("attachment", json.Content.Headers.ContentDisposition.DispositionType);
        Assert.Equal(award.Credential, await json.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(award.Jwt, await jwt.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NotFound, someoneElses.StatusCode);
    }

    [Fact]
    public async Task MyCredentials_TheLearnerChoosesToShowTheirName()
    {
        // Arrange
        var (award, _) = await AwardAsync("credpage3");
        using var http = await _fixture.CreateClientAsync("credpage3");
        var page = await http.GetStringAsync("learning/credentials", TestContext.Current.CancellationToken);

        // Act
        var response = await http.PostAsync($"learning/credentials/mine/{award.ItemId}/show-name", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["showName"] = "true",
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(page),
        }), TestContext.Current.CancellationToken);
        var stored = await _fixture.InTenantAsync(services => services.GetRequiredService<ICredentialAwardService>().FindByIdAsync(award.ItemId));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(stored.ShowName);
    }

    [Fact]
    public async Task Admin_TheAwardsList_TheAwardForm_AndTheRevocation()
    {
        // Arrange
        var (award, definitionId) = await AwardAsync("credpage4");
        var userId = await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, "credpage5"));
        using var http = await _fixture.CreateClientAsync("admin");

        // Act
        var list = await http.GetAsync("Admin/learning/credential-awards", TestContext.Current.CancellationToken);
        var listHtml = await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var filtered = await http.GetStringAsync("Admin/learning/credential-awards?Options.Status=revoked&Options.Search=credpage4", TestContext.Current.CancellationToken);
        var form = await http.GetStringAsync("Admin/learning/credential-awards/new", TestContext.Current.CancellationToken);
        var awarded = await http.PostAsync("Admin/learning/credential-awards/new", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["DefinitionId"] = definitionId,
            ["UserId"] = userId,
            ["Reason"] = "Practical assessment",
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(form),
        }), TestContext.Current.CancellationToken);
        var revokeForm = await http.GetStringAsync($"Admin/learning/credential-awards/{award.ItemId}/revoke", TestContext.Current.CancellationToken);
        var revoked = await http.PostAsync($"Admin/learning/credential-awards/{award.ItemId}/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Reason"] = "Given by mistake",
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(revokeForm),
        }), TestContext.Current.CancellationToken);
        var detail = await http.GetStringAsync($"Admin/learning/credential-awards/{award.ItemId}", TestContext.Current.CancellationToken);
        var newAwards = await _fixture.InTenantAsync(services => services.GetRequiredService<ICredentialAwardService>().ListForUserAsync(userId));

        // Assert
        Assert.True(list.StatusCode == HttpStatusCode.OK, listHtml);
        // The credential filter lists every definition, so the rows are found by their link.
        Assert.Contains($"learning/credential-awards/{award.ItemId}\"", listHtml, StringComparison.Ordinal);
        Assert.DoesNotContain($"learning/credential-awards/{award.ItemId}\"", filtered, StringComparison.Ordinal);
        Assert.Contains("action-bar-bottom", form, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, awarded.StatusCode);
        Assert.Equal("Practical assessment", Assert.Single(newAwards).AwardReason);
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
        Assert.Contains("data-award-status=\"Revoked\"", detail, StringComparison.Ordinal);
        Assert.Contains("Given by mistake", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_ALearner_CannotOpenTheAwardsList()
    {
        // Arrange
        await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, "credpage6"));
        using var http = await _fixture.CreateClientAsync("credpage6");

        // Act
        var list = await http.GetAsync("Admin/learning/credential-awards", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, list.StatusCode);
    }

    private async Task<(CredentialAward Award, string DefinitionId)> AwardAsync(string userName, string definitionId = null)
    {
        await _fixture.InTenantAsync(async services =>
        {
            var siteService = services.GetRequiredService<ISiteService>();
            var site = await siteService.LoadSiteSettingsAsync();
            var baseUrl = _fixture.BaseAddress.ToString().TrimEnd('/');

            if (site.BaseUrl != baseUrl)
            {
                site.BaseUrl = baseUrl;
                await siteService.UpdateSiteSettingsAsync(site);
            }
        });

        definitionId ??= await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var definition = await contentManager.NewAsync(CredentialsConstants.ContentTypes.CredentialDefinition);

            definition.DisplayText = "Badge of " + userName;
            definition.Alter<TitlePart>(part => part.Title = definition.DisplayText);
            definition.Alter<CredentialDefinitionPart>(part =>
            {
                part.Description = "A badge for the pages test.";
                part.Criteria = "Awarded by hand.";
            });
            await contentManager.CreateAsync(definition, VersionOptions.Published);

            return definition.ContentItemId;
        });

        var userId = await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, userName));
        var result = await _fixture.InTenantAsync(services => services.GetRequiredService<ICredentialAwardService>().AwardAsync(new CredentialAwardRequest
        {
            DefinitionId = definitionId,
            UserId = userId,
            RuleKind = CredentialsConstants.RuleKinds.Manual,
            Actor = "admin",
        }));

        Assert.True(result.Succeeded, result.ErrorCode);

        return (result.Award, definitionId);
    }
}
