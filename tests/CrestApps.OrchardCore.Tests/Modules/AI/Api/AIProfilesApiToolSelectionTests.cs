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
using CrestApps.OrchardCore.AI.Tools.Services;
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
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

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
        var result = await AIProfilesApiEndpoints.CreateAsync(body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

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
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

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
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

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
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

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
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

        // Assert: saved as checked, and the name, which links point to, stays whatever its case.
        Assert.IsType<Ok<AIProfile>>(result.Result);
        Assert.Equal(["listAIProfiles"], profile.GetSettings<AIProfilePostSessionSettings>().ToolNames);
        Assert.Equal("support", profile.Name);
    }

    [Fact]
    public async Task Create_RefusesToolsSentUnderALowerCaseInnerKey()
    {
        // Arrange (MD4): the handler builds Properties as a case-sensitive dictionary, so the check found no tools under
        // "functioninvocationmetadata"; the store reads the saved document back without regard to case and runs them.
        var manager = HandlerBackedManager(null);
        var body = BoundBody("""{ "Name": "support", "Properties": { "functioninvocationmetadata": { "Names": ["enableFeature"] } } }""");

        // Act
        var result = await AIProfilesApiEndpoints.CreateAsync(body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains("enableFeature", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        manager.Verify(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_RefusesPostSessionToolsSentUnderALowerCaseInnerKey()
    {
        // Arrange (MD4): the profile has no post-session settings yet, so there is no spelling to copy the inner key from.
        var profile = new AIProfile { ItemId = "profile-1", Name = "support" };
        var manager = HandlerBackedManager(profile);
        var body = BoundBody("""{ "settings": { "aiprofilepostsessionsettings": { "EnablePostSessionProcessing": true, "ToolNames": ["enableFeature"] } } }""");

        // Act
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

        // Assert: refused, and the profile as the store would read it back has no such tool.
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains("enableFeature", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        Assert.Empty(Reloaded(profile).GetSettings<AIProfilePostSessionSettings>()?.ToolNames ?? []);
    }

    [Fact]
    public async Task Create_SavesAToolTheCallerMayUse_SentUnderALowerCaseInnerKey()
    {
        // Arrange
        var manager = HandlerBackedManager(null);
        AIProfile created = null;
        manager.Setup(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()))
            .Callback((AIProfile profile, CancellationToken _) => created = profile)
            .Returns(ValueTask.CompletedTask);
        var body = BoundBody("""{ "Name": "support", "Properties": { "functioninvocationmetadata": { "Names": ["listAIProfiles"] } } }""");

        // Act
        var result = await AIProfilesApiEndpoints.CreateAsync(body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

        // Assert
        Assert.IsType<Created<AIProfile>>(result.Result);
        Assert.Equal(["listAIProfiles"], Reloaded(created).GetOrCreate<FunctionInvocationMetadata>().Names);
    }

    [Fact]
    public async Task Update_AppliesFlatSettingsSentInCamelCase()
    {
        // Arrange (MD4): the bound body matched "systemMessage" to the handler's "SystemMessage"; the normalized changes
        // must too, or a camel-cased flat setting is dropped silently.
        var profile = new AIProfile { ItemId = "profile-1", Name = "support" };
        var manager = HandlerBackedManager(profile);
        var body = BoundBody("""{ "systemMessage": "Be brief.", "isListable": false }""");

        // Act
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

        // Assert
        Assert.IsType<Ok<AIProfile>>(result.Result);
        Assert.Equal("Be brief.", profile.GetOrCreate<AIProfileMetadata>().SystemMessage);
        Assert.False(profile.GetOrCreateSettings<AIProfileSettings>().IsListable);
    }

    [Theory]
    [InlineData("""{ "systemMessage": "a", "SystemMessage": "b" }""")]
    [InlineData("""{ "Settings": { "AIProfilePostSessionSettings": { "ToolNames": ["listAIProfiles"] } }, "settings": { "aiprofilepostsessionsettings": { "toolNames": ["listAIProfiles"] } } }""")]
    public async Task Update_WithKeysThatDifferOnlyInCase_DoesNotThrow(string json)
    {
        // Arrange (MD4): two keys that differ only in case are one key, as the store reads them.
        var profile = new AIProfile { ItemId = "profile-1", Name = "support" };
        var manager = HandlerBackedManager(profile);
        var body = JsonNode.Parse(json)!.AsObject();

        // Act
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

        // Assert
        Assert.IsType<Ok<AIProfile>>(result.Result);
    }

    [Fact]
    public void BoundBody_WithKeysThatDifferOnlyInCase_Binds()
    {
        // MD4: the minimal API binds the body before the endpoint runs; two keys that differ only in case must not make
        // that throw, which would answer 500.
        var body = BoundBody("""{ "systemMessage": "a", "SystemMessage": "b", "Settings": { "A": 1 }, "settings": { "B": 2 } }""");

        Assert.NotNull(body);
    }

    [Fact]
    public async Task Create_RefusesAToolInstanceTheCallerMayNotUse()
    {
        // Arrange (review 7, F7): the check read only the tool names, so a tool instance the caller may not use was saved
        // and ran. The editor offers only the instances whose function the caller may use.
        var manager = HandlerBackedManager(null);
        var body = BoundBody("""{ "Name": "support", "Properties": { "AIToolInstanceMetadata": { "ToolInstanceNames": ["mailer"] } } }""");

        // Act
        var result = await AIProfilesApiEndpoints.CreateAsync(body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances("mailer", "lookup"));

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains("mailer", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        manager.Verify(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("ghost")]
    [InlineData("tool_instance_lookup")]
    public async Task Create_RefusesAToolInstanceNameThatIsNoAccessibleInstance(string instanceName)
    {
        // Arrange: an unknown name would run once an instance takes it, and a function name is not an instance name.
        var manager = HandlerBackedManager(null);
        var body = BoundBody($$"""{ "Name": "support", "Properties": { "AIToolInstanceMetadata": { "ToolInstanceNames": ["{{instanceName}}"] } } }""");

        // Act
        var result = await AIProfilesApiEndpoints.CreateAsync(body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances("mailer", "lookup"));

        // Assert
        Assert.IsType<ValidationProblem>(result.Result);
    }

    [Fact]
    public async Task Create_SavesAToolInstanceTheCallerMayUse()
    {
        // Arrange
        var manager = HandlerBackedManager(null);
        AIProfile created = null;
        manager.Setup(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()))
            .Callback((AIProfile profile, CancellationToken _) => created = profile)
            .Returns(ValueTask.CompletedTask);
        var body = BoundBody("""{ "Name": "support", "Properties": { "AIToolInstanceMetadata": { "ToolInstanceNames": ["lookup"] } } }""");

        // Act
        var result = await AIProfilesApiEndpoints.CreateAsync(body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances("mailer", "lookup"));

        // Assert
        Assert.IsType<Created<AIProfile>>(result.Result);
        Assert.Equal(["lookup"], Reloaded(created).GetOrCreate<AIToolInstanceMetadata>().ToolInstanceNames);
    }

    [Fact]
    public async Task Update_RefusesAPostSessionToolInstanceTheCallerMayNotUse()
    {
        // Arrange (F7): post-session work runs after the session with no user.
        var profile = new AIProfile { ItemId = "profile-1", Name = "support" };
        var manager = HandlerBackedManager(profile);
        var body = BoundBody("""{ "Settings": { "AIProfilePostSessionSettings": { "ToolInstanceNames": ["mailer"] } } }""");

        // Act
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances("mailer", "lookup"));

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains("mailer", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        Assert.Empty(Reloaded(profile).GetSettings<AIProfilePostSessionSettings>()?.ToolInstanceNames ?? []);
    }

    [Fact]
    public async Task Update_RefusesTheToolsOfAPostSessionTask()
    {
        // Arrange (F7): the package unions each task's tools and instances with the profile's post-session ones.
        var profile = new AIProfile { ItemId = "profile-1", Name = "support" };
        var manager = HandlerBackedManager(profile);
        var body = BoundBody("""
            { "Settings": { "AIProfilePostSessionSettings": { "EnablePostSessionProcessing": true, "PostSessionTasks": [
                { "Name": "summary", "Type": "Semantic", "Instructions": "Summarize.", "ToolNames": ["sendEmail"], "ToolInstanceNames": ["mailer"] }
            ] } } }
            """);

        // Act
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances("mailer", "lookup"));

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains("sendEmail", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        Assert.Contains("mailer", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        manager.Verify(m => m.UpdateAsync(It.IsAny<AIProfile>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("AIProfileFunctionInvocationMetadata", "Names")]
    [InlineData("aiprofilefunctioninvocationmetadata", "names")]
    public async Task Create_RefusesToolsUnderTheOlderFunctionInvocationKey(string key, string namesKey)
    {
        // Arrange (F7): on a profile with no tools the package falls back to this older settings key.
        var manager = HandlerBackedManager(null);
        var body = BoundBody($$"""{ "Name": "support", "Settings": { "{{key}}": { "{{namesKey}}": ["enableFeature"] } } }""");

        // Act
        var result = await AIProfilesApiEndpoints.CreateAsync(body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances());

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains("enableFeature", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        manager.Verify(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_KeepsAToolInstanceTheProfileAlreadyHad_AndAddsOneTheCallerMayUse()
    {
        // Arrange: a more powerful user gave the profile the mailer instance; saving it again must not need that right.
        var profile = new AIProfile { ItemId = "profile-1", Name = "support" };
        profile.Put(new AIToolInstanceMetadata { ToolInstanceNames = ["mailer"] });
        var manager = HandlerBackedManager(profile);
        var body = BoundBody("""{ "Properties": { "AIToolInstanceMetadata": { "ToolInstanceNames": ["mailer", "lookup"] } } }""");

        // Act
        var result = await AIProfilesApiEndpoints.UpdateAsync("profile-1", body, Context(), Authorization(), manager.Object, Evaluator("listAIProfiles"), Instances("mailer", "lookup"));

        // Assert
        Assert.IsType<Ok<AIProfile>>(result.Result);
    }

    // The profile as the store reads it back: the document's Properties and Settings are rebuilt without regard to case.
    private static AIProfile Reloaded(AIProfile profile)
        => JsonSerializer.Deserialize<AIProfile>(JsonSerializer.Serialize(profile));

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
        manager.Setup(m => m.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AIProfile)null);
        manager.Setup(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
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

    // The real accessor over a catalog of instances, as the editor uses it: an instance is offered when the caller may
    // use its function, "tool_instance_" and the instance name. The caller may use the lookup instance only.
    private static DefaultAIToolInstanceAccessor Instances(params string[] names)
    {
        var catalog = new Mock<ISourceCatalog<AIToolInstance>>();
        catalog.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => names.Select(name => new AIToolInstance { ItemId = name + "-id", Name = name, Source = "HttpApiRequest" }).ToList());

        return new DefaultAIToolInstanceAccessor(
            [catalog.Object],
            Evaluator("listAIProfiles", "tool_instance_lookup"),
            new HttpContextAccessor { HttpContext = Context() });
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
