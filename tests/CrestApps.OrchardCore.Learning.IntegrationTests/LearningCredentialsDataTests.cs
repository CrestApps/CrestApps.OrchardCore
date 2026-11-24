using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Recipes;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Settings;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Digital credentials' data on a running site: the <c>LearningCredentialAwards</c> deployment step exports awards as
/// learner records named by user name and the recipe step imports them as issued; the personal data export lists a
/// person's credentials and erasure revokes them.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningCredentialsDataTests
{
    private readonly LearningSiteFixture _fixture;

    public LearningCredentialsDataTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheDeploymentStep_ExportsAwardsByUserName_AndTheRecipeStep_ImportsThemAsIssued()
    {
        // Arrange
        var award = await AwardAsync("creddata1");

        // Act: export, delete the award, import the exported step again.
        var recipe = await _fixture.InTenantAsync(async services =>
        {
            var plan = new DeploymentPlan { Name = "Credential awards", DeploymentSteps = [new LearningCredentialAwardsDeploymentStep()] };
            var result = new DeploymentPlanResult(new MemoryFileBuilder(), new RecipeDescriptor { Name = "CredentialAwards" });

            await services.GetRequiredService<IDeploymentManager>().ExecuteDeploymentPlanAsync(plan, result);
            await result.FinalizeAsync();

            return result.Recipe;
        });

        var step = recipe["steps"]!.AsArray().OfType<JsonObject>().Single(candidate => candidate["name"]?.GetValue<string>() == CredentialsConstants.RecipeStepName);
        var row = step["Awards"].AsArray().OfType<JsonObject>().Single(item => item["PublicId"].GetValue<string>() == award.PublicId);

        await _fixture.InTenantAsync(async services =>
        {
            var store = services.GetRequiredService<CredentialAwardStore>();

            await store.DeleteAsync(await store.FindByIdAsync(award.ItemId));
        });

        await _fixture.InTenantAsync(services => LearningSiteFixture.RunRecipeAsync(services, new JsonObject
        {
            ["name"] = "CredentialAwardsImport",
            ["steps"] = new JsonArray(new JsonObject
            {
                ["name"] = CredentialsConstants.RecipeStepName,
                ["Awards"] = new JsonArray(row.DeepClone()),
            }),
        }));

        var imported = await _fixture.InTenantAsync(services => services.GetRequiredService<ICredentialAwardService>().FindByPublicIdAsync(award.PublicId));

        // Assert
        Assert.Equal("creddata1", row["RecipientUserName"].GetValue<string>());
        Assert.False(row.ContainsKey("RecipientUserId"));
        Assert.DoesNotContain("creddata1@example.com", step.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(imported);
        Assert.Equal(award.RecipientUserId, imported.RecipientUserId);
        Assert.Equal(award.Jwt, imported.Jwt);
        Assert.Equal(award.Credential, imported.Credential);
        Assert.Equal(award.StatusListIndex, imported.StatusListIndex);
    }

    [Fact]
    public async Task PersonalData_ExportsTheCredentials_AndErasureRevokesThem()
    {
        // Arrange
        var award = await AwardAsync("creddata2");

        // Act
        var exported = await _fixture.InTenantAsync(services => services.GetRequiredService<IPersonalDataService>().ExportAsync(new PersonalDataSubject(award.RecipientUserId), TestContext.Current.CancellationToken));
        var erased = await _fixture.InTenantAsync(services => services.GetRequiredService<IPersonalDataService>().AnonymizeAsync(new PersonalDataSubject(award.RecipientUserId), "erased", TestContext.Current.CancellationToken));
        var stored = await _fixture.InTenantAsync(services => services.GetRequiredService<CredentialAwardStore>().FindByIdAsync(award.ItemId).AsTask());

        // Assert
        var record = Assert.Single(exported.Records, item => item.Kind == "LearningCredentialAward");

        Assert.Equal(award.Jwt, record.Fields["SignedCredential"]);
        Assert.Null(stored.RecipientUserId);
        Assert.Null(stored.Jwt);
        Assert.True(stored.IsRevoked);
        Assert.Contains(erased.Warnings, warning => warning.Contains("cannot be reached", StringComparison.Ordinal));
    }

    private async Task<CredentialAward> AwardAsync(string userName)
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

        var definitionId = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var definition = await contentManager.NewAsync(CredentialsConstants.ContentTypes.CredentialDefinition);

            definition.DisplayText = "Data badge " + userName;
            definition.Alter<TitlePart>(part => part.Title = definition.DisplayText);
            definition.Alter<CredentialDefinitionPart>(part =>
            {
                part.Description = "A badge for the data test.";
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

        return result.Award;
    }

    private sealed class MemoryFileBuilder : IFileBuilder
    {
        public Task SetFileAsync(string subpath, Stream stream)
            => Task.CompletedTask;
    }
}
