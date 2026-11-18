using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Deployments;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.Services;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OrchardCore;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Services;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// External tools' data on a running site: the <c>LearningExternalTools</c> deployment step exports the tools' settings
/// with their subject salt and the recipe step imports them, so the ids a tool knows people by stay the same; personal
/// data exports a person's scores and the id each tool knows them by, and erasure removes the scores.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LtiDataTests
{
    private readonly LearningSiteFixture _fixture;

    public LtiDataTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Admin_TheToolsListAndTheNewToolEditor_RenderForTheAdministrator()
    {
        // Arrange
        using var http = await _fixture.CreateClientAsync("admin");

        // Act
        var list = await http.GetAsync("Admin/learning/external-tools", TestContext.Current.CancellationToken);
        var editor = await http.GetAsync("Admin/learning/external-tools/create", TestContext.Current.CancellationToken);
        var html = await editor.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, list.StatusCode);
        Assert.True(editor.StatusCode == System.Net.HttpStatusCode.OK, html);
        Assert.Contains("Give the tool these platform details", html, StringComparison.Ordinal);
        Assert.Contains("learning/lti/jwks", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validation_TheEditorsRedirectUrisAndKeySet_AreCheckedByTheToolsHandler_AndARecipeNeedsOnlyTheSettings()
    {
        // Arrange and act
        var (badInput, nothing, recipe) = await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<IExternalToolManager>();
            var clients = services.GetRequiredService<ExternalToolClients>();

            async Task<ExternalTool> NewToolAsync()
            {
                var tool = await manager.NewAsync();

                tool.Name = "Checked tool";
                tool.ClientId = "lti-check-" + Guid.NewGuid().ToString("n");
                tool.DeploymentId = "deployment-check";
                tool.LoginInitiationUrl = "https://check.example.com/login";
                tool.TargetLinkUri = "https://check.example.com/launch";

                return tool;
            }

            var edited = await NewToolAsync();
            clients.SetPending(edited, ["not an address", "https://check.example.com/launch#fragment"], "{\"keys\":[]}");
            var first = await manager.ValidateAsync(edited);

            clients.SetPending(edited, [], null);
            var second = await manager.ValidateAsync(edited);

            var imported = await manager.ValidateAsync(await NewToolAsync());

            return (first, second, imported);
        });

        // Assert
        Assert.False(badInput.Succeeded);
        Assert.Equal(2, badInput.Errors.Count(error => error.MemberNames.Contains("RedirectUris")));
        Assert.Contains(badInput.Errors, error => error.MemberNames.Contains("KeySet"));
        Assert.False(nothing.Succeeded);
        Assert.Contains(nothing.Errors, error => error.MemberNames.Contains("RedirectUris"));
        Assert.Contains(nothing.Errors, error => error.MemberNames.Contains(nameof(ExternalTool.JwksUrl)));
        Assert.True(recipe.Succeeded);
    }

    [Fact]
    public async Task Round9H2_AToolWithTheClientIdOfAnotherOpenIdApplication_IsRefused_AndTheApplicationStaysAsItWas()
    {
        // Arrange: a confidential client with the client credentials grant and the Administrator role, which External
        // Tools did not make.
        var clientId = "foreign-client-" + Guid.NewGuid().ToString("n");

        await _fixture.InTenantAsync(async services =>
        {
            var descriptor = new OpenIdApplicationDescriptor
            {
                ClientId = clientId,
                ClientSecret = "Foreign-secret-1234567890",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
                DisplayName = "Foreign client",
            };

            descriptor.Roles.Add("Administrator");
            descriptor.RedirectUris.Add(new Uri("https://foreign.example.com/callback"));
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);

            await services.GetRequiredService<IOpenIdApplicationManager>().CreateAsync(descriptor, TestContext.Current.CancellationToken);
        });

        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(false);
        var keySet = "{\"keys\":[{\"kty\":\"RSA\",\"use\":\"sig\",\"kid\":\"attacker-1\",\"n\":\"" + Base64UrlEncoder.Encode(parameters.Modulus) + "\",\"e\":\"" + Base64UrlEncoder.Encode(parameters.Exponent) + "\"}]}";

        // Act: someone who manages external tools registers a tool with that client id, their own redirect URI and
        // key set, and the editor saves it.
        var (validation, syncRefused) = await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<IExternalToolManager>();
            var clients = services.GetRequiredService<ExternalToolClients>();
            var tool = await manager.NewAsync();

            tool.Name = "Takeover tool";
            tool.ClientId = clientId;
            tool.DeploymentId = "deployment-takeover";
            tool.LoginInitiationUrl = "https://evil.example.com/login";
            tool.TargetLinkUri = "https://evil.example.com/launch";
            clients.SetPending(tool, ["https://evil.example.com/launch"], keySet);

            var result = await manager.ValidateAsync(tool);
            var refused = false;

            try
            {
                await clients.SyncAsync(tool);
            }
            catch (InvalidOperationException)
            {
                refused = true;
            }

            return (result, refused);
        });

        var (redirectUris, consentType, storedKeySet, roles, marked) = await _fixture.InTenantAsync(async services =>
        {
            var applications = services.GetRequiredService<IOpenIdApplicationManager>();
            var application = await applications.FindByClientIdAsync(clientId, TestContext.Current.CancellationToken);

            return (
                (await applications.GetRedirectUrisAsync(application, TestContext.Current.CancellationToken)).ToArray(),
                await applications.GetConsentTypeAsync(application, TestContext.Current.CancellationToken),
                await applications.GetJsonWebKeySetAsync(application, TestContext.Current.CancellationToken),
                (await applications.GetRolesAsync(application, TestContext.Current.CancellationToken)).ToArray(),
                (await applications.GetPropertiesAsync(application, TestContext.Current.CancellationToken)).ContainsKey(ExternalToolClients.MarkerProperty));
        });

        // Assert
        Assert.False(validation.Succeeded);
        Assert.Contains(validation.Errors, error => error.MemberNames.Contains(nameof(ExternalTool.ClientId)));
        Assert.True(syncRefused);
        Assert.Equal(["https://foreign.example.com/callback"], redirectUris);
        Assert.Equal(OpenIddictConstants.ConsentTypes.Explicit, consentType);
        Assert.Null(storedKeySet);
        Assert.Contains("Administrator", roles);
        Assert.False(marked);
    }

    [Fact]
    public async Task Recipe_TheExportedStep_ImportsTheToolAgain_WithTheSameIds()
    {
        // Arrange: a tool of its own, so the shared one stays.
        var clientId = "lti-data-" + Guid.NewGuid().ToString("n");
        var (toolId, subject) = await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<IExternalToolManager>();
            var tool = await manager.NewAsync();

            tool.Name = "Recipe tool";
            tool.ClientId = clientId;
            tool.DeploymentId = "deployment-recipe";
            tool.LoginInitiationUrl = "https://recipe.example.com/login";
            tool.TargetLinkUri = "https://recipe.example.com/launch";
            tool.PrivacyLevel = LtiPrivacyLevel.Name;
            tool.AllowGrades = true;

            await manager.CreateAsync(tool);

            return (tool.ItemId, LtiSubjects.For(tool, _fixture.EnrolledUserId));
        });

        var recipe = await _fixture.InTenantAsync(async services =>
        {
            var plan = new DeploymentPlan { Name = "External tools", DeploymentSteps = [new LearningExternalToolsDeploymentStep()] };
            var result = new DeploymentPlanResult(new MemoryFileBuilder(), new RecipeDescriptor { Name = "ExternalTools" });

            await services.GetRequiredService<IDeploymentManager>().ExecuteDeploymentPlanAsync(plan, result);
            await result.FinalizeAsync();

            return result.Recipe;
        });

        var step = recipe["steps"]!.AsArray().OfType<JsonObject>().Single(candidate => candidate["name"]?.GetValue<string>() == LtiConstants.RecipeStepName);
        var row = step["Tools"]!.AsArray().OfType<JsonObject>().Single(candidate => candidate["ClientId"]?.GetValue<string>() == clientId);

        // Act: remove the tool, then run the exported step.
        await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<IExternalToolManager>();

            await manager.DeleteAsync(await manager.FindByIdAsync(toolId));
        });

        await _fixture.InTenantAsync(services => LearningSiteFixture.RunRecipeAsync(services, new JsonObject
        {
            ["name"] = "ExternalToolsImport",
            ["steps"] = new JsonArray(new JsonObject
            {
                ["name"] = LtiConstants.RecipeStepName,
                ["Tools"] = new JsonArray(row.DeepClone()),
            }),
        }));

        // Assert
        var imported = await _fixture.InTenantAsync(services => services.GetRequiredService<IExternalToolStore>().FindByClientIdAsync(clientId, TestContext.Current.CancellationToken));

        Assert.NotNull(imported);
        Assert.Equal(toolId, imported.ItemId);
        Assert.Equal("Recipe tool", imported.Name);
        Assert.Equal("deployment-recipe", imported.DeploymentId);
        Assert.Equal(LtiPrivacyLevel.Name, imported.PrivacyLevel);
        Assert.True(imported.AllowGrades);
        Assert.Equal(subject, LtiSubjects.For(imported, _fixture.EnrolledUserId));
    }

    [Fact]
    public async Task PersonalData_ExportsTheScoresAndTheIdsTools_KnowThePersonBy_AndErasureRemovesTheScores()
    {
        // Arrange: a score of a learner who is not the shared fixture's.
        await LtiLaunchTests.EnsureToolAndItemAsync(_fixture);

        var userId = await _fixture.InTenantAsync(services => LearningSiteFixture.CreateUserAsync(services, string.Concat("lti-data-", Guid.NewGuid().ToString("n").AsSpan(0, 8))));
        var subject = await _fixture.InTenantAsync(async services =>
        {
            var tool = await services.GetRequiredService<IExternalToolStore>().FindByClientIdAsync(LtiLaunchTests.ClientId, TestContext.Current.CancellationToken);

            await services.GetRequiredService<LtiScoreStore>().CreateAsync(new LtiScore
            {
                ItemId = IdGenerator.GenerateId(),
                LineItemId = "line-data",
                ToolId = tool.ItemId,
                CourseId = _fixture.CourseId,
                UserId = userId,
                Subject = LtiSubjects.For(tool, userId),
                ScoreGiven = 7,
                ScoreMaximum = 10,
                ActivityProgress = "Completed",
                GradingProgress = "FullyGraded",
                TimestampUtc = DateTime.UtcNow,
                ReceivedUtc = DateTime.UtcNow,
            });

            return LtiSubjects.For(tool, userId);
        });

        // Act
        var export = await _fixture.InTenantAsync(services => services.GetRequiredService<IPersonalDataService>().ExportAsync(new PersonalDataSubject(userId), TestContext.Current.CancellationToken));
        var erased = await _fixture.InTenantAsync(services => services.GetRequiredService<IPersonalDataService>().AnonymizeAsync(new PersonalDataSubject(userId), "erased", TestContext.Current.CancellationToken));
        var remaining = await _fixture.InTenantAsync(services => services.GetRequiredService<LtiScoreStore>().ListByUserAsync(userId, TestContext.Current.CancellationToken));

        // Assert
        var score = Assert.Single(export.Records, record => record.Kind == "LearningLtiScore");
        Assert.Equal("7", score.Fields["ScoreGiven"]);
        Assert.Contains(export.Records, record => record.Kind == "LearningLtiId" && record.Fields["Id"] == subject);
        Assert.Empty(remaining);
        Assert.Contains(erased.Warnings, warning => warning.Contains("External tools keep their own records", StringComparison.Ordinal));
    }

    private sealed class MemoryFileBuilder : IFileBuilder
    {
        public Task SetFileAsync(string subpath, Stream stream)
            => Task.CompletedTask;
    }
}
