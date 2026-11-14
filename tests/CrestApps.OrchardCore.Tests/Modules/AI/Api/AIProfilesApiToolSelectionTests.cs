using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Endpoints.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Api;

/// <summary>
/// SEC-A3 step 1: the profiles API takes raw JSON, so it applies the editor's rule to every list of tools a profile runs.
/// The post-session tools run after the session with no user and no caller check, so a caller with Manage AI profiles
/// must not be able to give a profile a post-session tool they may not use themselves.
/// </summary>
public sealed class AIProfilesApiToolSelectionTests
{
    [Fact]
    public async Task Update_RefusesPostSessionToolsTheCallerMayNotUse()
    {
        // Arrange
        var profile = new AIProfile { ItemId = "profile-1", Name = "support" };
        var manager = Manager(profile);
        var body = PostSessionTools("sendEmail", "enableFeature");

        // Act
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"));

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains("sendEmail", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        Assert.Contains("enableFeature", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        manager.Verify(m => m.UpdateAsync(It.IsAny<AIProfile>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_RefusesPostSessionToolsTheCallerMayNotUse()
    {
        // Arrange
        var manager = Manager(null);
        var body = PostSessionTools("sendEmail");
        body["Name"] = "support";

        // Act
        var result = await AIProfilesApiEndpoints.CreateAsync(body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"));

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains("sendEmail", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        manager.Verify(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_KeepsAPostSessionToolTheProfileAlreadyHad_AndAddsOneTheCallerMayUse()
    {
        // Arrange: a more powerful user gave the profile sendEmail; saving it again must not need that right.
        var profile = new AIProfile { ItemId = "profile-1", Name = "support" };
        profile.AlterSettings<AIProfilePostSessionSettings>(settings => settings.ToolNames = ["sendEmail"]);
        var manager = Manager(profile);
        var body = PostSessionTools("sendEmail", "listAIProfiles");

        // Act
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"));

        // Assert
        Assert.IsType<Ok<AIProfile>>(result.Result);
        manager.Verify(m => m.UpdateAsync(profile, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("settings")]
    [InlineData("SETTINGS")]
    public async Task Update_RefusesPostSessionToolsSentUnderAKeyInAnotherCase(string settingsKey)
    {
        // Arrange (M4): the body is bound without regard to case, the profile's own JSON is not. The check read the old
        // "Settings" of the merged copy while the package's handler saved the new "settings" from the body.
        var profile = new AIProfile { ItemId = "profile-1", Name = "support" };
        var manager = HandlerBackedManager(profile);
        var body = BoundBody($$"""{ "{{settingsKey}}": { "AIProfilePostSessionSettings": { "ToolNames": ["sendEmail"] } } }""");

        // Act
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"));

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains("sendEmail", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        Assert.Empty(profile.GetSettings<AIProfilePostSessionSettings>()?.ToolNames ?? []);
    }

    [Fact]
    public async Task Update_RefusesToolsSentUnderALowerCasePropertiesKey()
    {
        // Arrange
        var profile = new AIProfile { ItemId = "profile-1", Name = "support" };
        var manager = HandlerBackedManager(profile);
        var body = BoundBody("""{ "properties": { "FunctionInvocationMetadata": { "Names": ["enableFeature"] } } }""");

        // Act
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"));

        // Assert
        Assert.IsType<ValidationProblem>(result.Result);
        Assert.Empty(profile.GetOrCreate<FunctionInvocationMetadata>().Names ?? []);
    }

    [Fact]
    public async Task Update_SavesAToolTheCallerMayUse_SentUnderALowerCaseKey()
    {
        // Arrange
        var profile = new AIProfile { ItemId = "profile-1", Name = "support" };
        var manager = HandlerBackedManager(profile);
        var body = BoundBody("""{ "settings": { "AIProfilePostSessionSettings": { "ToolNames": ["listAIProfiles"] } }, "name": "renamed" }""");

        // Act
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"));

        // Assert: saved as checked, and the name, which links point to, stays whatever its case.
        Assert.IsType<Ok<AIProfile>>(result.Result);
        Assert.Equal(["listAIProfiles"], profile.GetSettings<AIProfilePostSessionSettings>().ToolNames);
        Assert.Equal("support", profile.Name);
    }

    // The minimal API binds a JsonObject body with the web defaults, which match property names without regard to case.
    private static JsonObject BoundBody(string json)
        => JsonSerializer.Deserialize<JsonObject>(json, JsonSerializerOptions.Web);

    // A manager whose NewAsync and UpdateAsync run the package's own AIProfileHandler, as the real catalog manager does,
    // so the copy that is checked and the update that is saved read the JSON the way production reads it.
    private static Mock<IAIProfileManager> HandlerBackedManager(AIProfile existing)
    {
        var handlerType = typeof(CrestApps.Core.AI.Services.DefaultOAuth2TokenService).Assembly.GetType("CrestApps.Core.AI.Handlers.AIProfileHandler", throwOnError: true);
        var handler = (ICatalogEntryHandler<AIProfile>)Activator.CreateInstance(
            handlerType,
            new HttpContextAccessor(),
            TimeProvider.System,
            Mock.Of<IAIProfileStore>(),
            Mock.Of<IAIDeploymentStore>(),
            null);

        var manager = new Mock<IAIProfileManager>();
        manager.Setup(m => m.FindByIdAsync("profile-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        manager.Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(async (JsonNode data, CancellationToken cancellationToken) =>
            {
                var profile = new AIProfile();
                await handler.InitializingAsync(new InitializingContext<AIProfile>(profile, data), cancellationToken);

                return profile;
            });
        manager.Setup(m => m.ValidateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ValidationResultDetails());
        manager.Setup(m => m.UpdateAsync(It.IsAny<AIProfile>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(async (AIProfile profile, JsonNode data, CancellationToken cancellationToken) =>
                await handler.UpdatingAsync(new UpdatingContext<AIProfile>(profile, data), cancellationToken));

        return manager;
    }

    private static JsonObject PostSessionTools(params string[] names)
        => new()
        {
            ["Settings"] = new JsonObject
            {
                [nameof(AIProfilePostSessionSettings)] = new JsonObject
                {
                    [nameof(AIProfilePostSessionSettings.ToolNames)] = new JsonArray([.. names.Select(name => (JsonNode)name)]),
                },
            },
        };

    // The catalog manager builds a profile from the posted JSON; the endpoint validates that copy before it saves.
    private static Mock<IAIProfileManager> Manager(AIProfile existing)
    {
        var manager = new Mock<IAIProfileManager>();
        manager.Setup(m => m.FindByIdAsync("profile-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        manager.Setup(m => m.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AIProfile)null);
        manager.Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JsonNode data, CancellationToken _) => data.Deserialize<AIProfile>() ?? new AIProfile());
        manager.Setup(m => m.ValidateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ValidationResultDetails());
        manager.Setup(m => m.UpdateAsync(It.IsAny<AIProfile>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        manager.Setup(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        return manager;
    }

    private static IAIToolAccessEvaluator Evaluator(params string[] allowed)
    {
        var evaluator = new Mock<IAIToolAccessEvaluator>();
        evaluator.Setup(e => e.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, string tool) => allowed.Contains(tool, StringComparer.Ordinal));

        return evaluator.Object;
    }

    private static IAuthorizationService Authorization()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return authorization.Object;
    }

    private static DefaultHttpContext Context()
        => new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "manager")], "Test")) };
}
