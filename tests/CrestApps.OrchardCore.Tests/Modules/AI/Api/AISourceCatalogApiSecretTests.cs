using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.Recipes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Recipes.Models;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Api;

/// <summary>
/// MD5 (fix verification, round 6): the editors and recipe steps drop a stored secret when its address or way of signing
/// in changes, but the update endpoint of the data source, file source and web crawler API did not. A read returns the
/// record with its protected secrets, so a caller who may manage the records, but never saw the key, could send that
/// protected value back with an endpoint of their own; the stored key was kept and sent there with the next call.
/// </summary>
public sealed class AISourceCatalogApiSecretTests
{
    private const string Id = "4ds00000000000000000000001";
    private const string MetadataName = "AzureAISearchSourceMetadata";
    private const string StoredKey = "CfDJ8-protected-key";
    private const string ConnectionId = "4qkc7h2m9x1r5t8v3w6y0z2b4d";

    private readonly EphemeralDataProtectionProvider _protection = new();

    [Fact]
    public async Task Update_DropsTheKey_WhenTheBodySendsTheProtectedKeyBackWithAnotherEndpoint()
    {
        // Arrange
        var manager = Manager(Stored("https://search.vendor.example/"));
        var body = Body("https://attacker.example/", "ApiKey", StoredKey);

        // Act
        var result = await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, Secrets(), AIPermissions.ManageAIDataSources);

        // Assert
        Assert.IsType<Ok<AIDataSource>>(result.Result);
        var saved = manager.LastSaved;
        Assert.Equal("https://attacker.example/", saved[MetadataName]["Endpoint"].GetValue<string>());
        Assert.Null(saved[MetadataName]["ApiKey"]);
        Assert.Null(Metadata(manager.Entries.Single())["ApiKey"]);

        // The drop is saved: a store that wrote the record on the first save writes it again.
        Assert.Equal(2, manager.Saves);
    }

    [Fact]
    public async Task Update_DropsTheKey_WhenTheAuthenticationTypeChanges()
    {
        // Arrange
        var manager = Manager(Stored("https://search.vendor.example/"));
        var body = Body("https://search.vendor.example/", "Default", StoredKey);

        // Act
        await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, Secrets(), AIPermissions.ManageAIDataSources);

        // Assert
        Assert.Null(manager.LastSaved[MetadataName]["ApiKey"]);
    }

    [Fact]
    public async Task Update_KeepsTheKey_WhenOnlyThePathChanges()
    {
        // Arrange
        var manager = Manager(Stored("https://search.vendor.example/v1"));
        var body = Body("https://search.vendor.example/v2", "ApiKey", StoredKey);

        // Act
        var result = await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, Secrets(), AIPermissions.ManageAIDataSources);

        // Assert
        Assert.IsType<Ok<AIDataSource>>(result.Result);
        Assert.Equal(StoredKey, manager.LastSaved[MetadataName]["ApiKey"].GetValue<string>());
        Assert.Equal(1, manager.Saves);
    }

    [Fact]
    public async Task Update_KeepsANewKey_SentWithAnotherEndpoint()
    {
        // Arrange
        var manager = Manager(Stored("https://search.vendor.example/"));
        var body = Body("https://search.other.example/", "ApiKey", "new-key");

        // Act
        await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, Secrets(), AIPermissions.ManageAIDataSources);

        // Assert: kept, and protected (F9).
        Assert.Equal("new-key", Unprotect(manager.LastSaved[MetadataName]["ApiKey"].GetValue<string>()));
    }

    [Fact]
    public async Task Update_ReportsARequiredKey_ThatTheMoveDropped_AndSavesNothing()
    {
        // Arrange
        var manager = Manager(Stored("https://search.vendor.example/"));
        manager.RequireKey = true;
        var body = Body("https://attacker.example/", "ApiKey", StoredKey);

        // Act
        var result = await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, Secrets(), AIPermissions.ManageAIDataSources);

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.True(problem.ProblemDetails.Errors.ContainsKey("ApiKey"));
        Assert.Equal(0, manager.Saves);
        Assert.Equal(StoredKey, Metadata(manager.Entries.Single())["ApiKey"].GetValue<string>());
    }

    [Fact]
    public async Task Get_LeavesTheSecretsOut()
    {
        // Arrange (review 7, F9): the protected key a read returned was all a caller needed to send the key elsewhere.
        var manager = Manager(Stored("https://search.vendor.example/"));

        // Act
        var result = await AISourceCatalogApi.GetAsync(Id, Context(), Authorization(), manager, AIPermissions.ManageAIDataSources);

        // Assert
        var read = Assert.IsType<Ok<AIDataSource>>(result.Result).Value;
        var json = Serialized(read);
        Assert.DoesNotContain(StoredKey, json, StringComparison.Ordinal);
        Assert.Contains("https://search.vendor.example/", json, StringComparison.Ordinal);
        Assert.Equal(StoredKey, Metadata(manager.Entries.Single())["ApiKey"].GetValue<string>());
    }

    [Fact]
    public async Task List_LeavesTheSecretsOut()
    {
        // Arrange
        var manager = Manager(Stored("https://search.vendor.example/"));

        // Act
        var result = await AISourceCatalogApi.ListAsync(null, Context(), Authorization(), manager, AIPermissions.ManageAIDataSources);

        // Assert
        var read = Assert.IsType<Ok<IReadOnlyList<AIDataSource>>>(result.Result).Value;
        Assert.DoesNotContain(StoredKey, Serialized(Assert.Single(read)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_RefusesAKeyThatIsAlreadyProtected()
    {
        // Arrange (F9): the protected key of one data source, given to a new one that points at the caller's host.
        var manager = new InMemorySources();
        var body = Body("https://attacker.example/", "ApiKey", Protected("the-real-key"));
        body["Source"] = "AzureAISearch";
        body["DisplayText"] = "Mine";

        // Act
        var result = await AISourceCatalogApi.CreateAsync(body, Context(), Authorization(), manager, Secrets(), "data-sources", "data source", AIPermissions.ManageAIDataSources);

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains(problem.ProblemDetails.Errors.Keys, key => key.EndsWith("ApiKey", StringComparison.Ordinal));
        Assert.Empty(manager.Entries);
    }

    [Fact]
    public async Task Create_ProtectsAPlainKey()
    {
        // Arrange (F9): a key written through the API was stored in clear.
        var manager = new InMemorySources();
        var body = Body("https://search.vendor.example/", "ApiKey", "plain-key");
        body["Source"] = "AzureAISearch";
        body["DisplayText"] = "Policies";

        // Act
        var result = await AISourceCatalogApi.CreateAsync(body, Context(), Authorization(), manager, Secrets(), "data-sources", "data source", AIPermissions.ManageAIDataSources);

        // Assert
        Assert.IsType<Created<AIDataSource>>(result.Result);
        var stored = Metadata(Assert.Single(manager.Entries))["ApiKey"].GetValue<string>();
        Assert.NotEqual("plain-key", stored);
        Assert.Equal("plain-key", Unprotect(stored));
    }

    [Fact]
    public async Task Update_RefusesAKeyProtectedForAnotherRecord_AndSavesNothing()
    {
        // Arrange (F9): the same works by PUT on any other record, even one that keeps its endpoint.
        var manager = Manager(Stored("https://search.vendor.example/"));
        var body = Body("https://search.vendor.example/", "ApiKey", Protected("another-records-key"));

        // Act
        var result = await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, Secrets(), AIPermissions.ManageAIDataSources);

        // Assert
        Assert.IsType<ValidationProblem>(result.Result);
        Assert.Equal(0, manager.Saves);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Update_KeepsTheStoredKey_WhenTheBodyLeavesItOutOrBlank(string key)
    {
        // Arrange (F9): a read no longer returns the key, so an update made from a read sends none. The package's handler
        // replaces every property with the posted ones.
        var manager = Manager(Stored("https://search.vendor.example/"));
        var body = Body("https://search.vendor.example/", "ApiKey", key);
        body["Properties"][MetadataName]["IndexName"] = "policies-v2";

        if (key is null)
        {
            body["Properties"][MetadataName].AsObject().Remove("ApiKey");
        }

        // Act
        var result = await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, Secrets(), AIPermissions.ManageAIDataSources);

        // Assert
        Assert.IsType<Ok<AIDataSource>>(result.Result);
        Assert.Equal(StoredKey, manager.LastSaved[MetadataName]["ApiKey"].GetValue<string>());
        Assert.Equal("policies-v2", manager.LastSaved[MetadataName]["IndexName"].GetValue<string>());
    }

    [Fact]
    public async Task Update_KeepsTheOtherProperties_WhenTheBodySendsOne()
    {
        // Arrange: "leaving the rest as they are" held for the record's own fields only; a body with one metadata object
        // replaced every other one.
        var stored = Stored("https://search.vendor.example/");
        stored.Properties["DataConnectionReference"] = new JsonObject { ["Note"] = "kept" };
        var manager = Manager(stored);
        var body = new JsonObject { ["Properties"] = new JsonObject { [MetadataName] = new JsonObject { ["IndexName"] = "policies-v2" } } };

        // Act
        await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, Secrets(), AIPermissions.ManageAIDataSources);

        // Assert
        Assert.Equal("kept", manager.LastSaved["DataConnectionReference"]["Note"].GetValue<string>());
        Assert.Equal(StoredKey, manager.LastSaved[MetadataName]["ApiKey"].GetValue<string>());
        Assert.Equal("https://search.vendor.example/", manager.LastSaved[MetadataName]["Endpoint"].GetValue<string>());
    }

    [Fact]
    public async Task RecipeStep_ProtectsAPlainKey()
    {
        // Arrange (F9): a key written through the AIDataSource recipe step was stored in clear.
        var manager = new InMemorySources();
        var options = new AIDataSourceSourceOptions();
        options.AddOrUpdate("AzureAISearch", new("Azure AI Search", "Azure AI Search"), new("Azure AI Search index.", "Azure AI Search index."));
        var step = new AIDataSourceStep(manager, Options.Create(options), _protection, Options.Create(SecretOptions()), new PassThroughStringLocalizer<AIDataSourceStep>());
        var entry = Body("https://search.vendor.example/", "ApiKey", "plain-key");
        entry["Source"] = "AzureAISearch";
        entry["DisplayText"] = "Policies";
        var context = new RecipeExecutionContext
        {
            Name = AIDataSourceStep.StepKey,
            Step = new JsonObject { ["name"] = AIDataSourceStep.StepKey, ["DataSources"] = new JsonArray(entry) },
        };

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        var stored = Metadata(Assert.Single(manager.Entries))["ApiKey"].GetValue<string>();
        Assert.Equal("plain-key", Unprotect(stored));
    }

    [Fact]
    public async Task RecipeStep_KeepsAKeyThatIsAlreadyProtected()
    {
        // Arrange: a recipe is trusted, and one made on this site may carry the protected value.
        var manager = new InMemorySources();
        var options = new AIDataSourceSourceOptions();
        options.AddOrUpdate("AzureAISearch", new("Azure AI Search", "Azure AI Search"), new("Azure AI Search index.", "Azure AI Search index."));
        var step = new AIDataSourceStep(manager, Options.Create(options), _protection, Options.Create(SecretOptions()), new PassThroughStringLocalizer<AIDataSourceStep>());
        var key = Protected("the-key");
        var entry = Body("https://search.vendor.example/", "ApiKey", key);
        entry["Source"] = "AzureAISearch";
        entry["DisplayText"] = "Policies";
        var context = new RecipeExecutionContext
        {
            Name = AIDataSourceStep.StepKey,
            Step = new JsonObject { ["name"] = AIDataSourceStep.StepKey, ["DataSources"] = new JsonArray(entry) },
        };

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Equal(key, Metadata(Assert.Single(manager.Entries))["ApiKey"].GetValue<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_AnswersWithoutTheStoredKey_InClearOrProtected(bool isProtected)
    {
        // Arrange (review 8, G2): GET and the list left the secrets out, but PUT answered with the record as saved. A key
        // written in clear before keys were protected came back in clear to a caller who renamed the record.
        var key = isProtected ? Protected("the-stored-key") : "the-stored-key";
        var manager = Manager(Stored("https://search.vendor.example/", key));
        var body = new JsonObject { ["DisplayText"] = "Renamed" };

        // Act
        var result = await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, Secrets(), AIPermissions.ManageAIDataSources);
        var response = await ResponseAsync(result.Result);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("Renamed", response.Body, StringComparison.Ordinal);
        Assert.Contains("https://search.vendor.example/", response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("the-stored-key", response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(key, response.Body, StringComparison.Ordinal);
        Assert.Equal(key, Metadata(manager.Entries.Single())["ApiKey"].GetValue<string>());
    }

    [Fact]
    public async Task Create_AnswersWithoutTheKey_ItWasGivenOrTheOneItStored()
    {
        // Arrange (review 8, G2): POST answered with the record as saved, protected key included.
        var manager = new InMemorySources();
        var body = Body("https://search.vendor.example/", "ApiKey", "plain-key");
        body["Source"] = "AzureAISearch";
        body["DisplayText"] = "Policies";

        // Act
        var result = await AISourceCatalogApi.CreateAsync(body, Context(), Authorization(), manager, Secrets(), "data-sources", "data source", AIPermissions.ManageAIDataSources);
        var response = await ResponseAsync(result.Result);

        // Assert
        var stored = Metadata(Assert.Single(manager.Entries))["ApiKey"].GetValue<string>();
        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        Assert.Contains("https://search.vendor.example/", response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("plain-key", response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(stored, response.Body, StringComparison.Ordinal);
        Assert.Equal("plain-key", Unprotect(stored));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_BindsADataConnection_OnlyForSomeoneWhoMayManageDataConnections(bool mayManageConnections)
    {
        // Arrange (review 8, G6): the editor lets only people who may manage data connections pick one, because a data
        // source that uses a connection reads with its credentials; the API bound any connection id it was given.
        var manager = new InMemorySources();
        var body = Body("https://search.vendor.example/", "ApiKey", "plain-key");
        body["Source"] = "AzureAISearch";
        body["DisplayText"] = "Mine";
        body["Properties"]["DataConnectionReference"] = new JsonObject { ["ConnectionId"] = ConnectionId };
        var context = Context();

        // Act
        var result = await AISourceCatalogApi.CreateAsync(body, context, Authorization(), manager, Secrets(), "data-sources", "data source", AIPermissions.ManageAIDataSources, Picker(context, mayManageConnections));

        // Assert
        if (mayManageConnections)
        {
            Assert.IsType<Created<AIDataSource>>(result.Result);
            Assert.Single(manager.Entries);
        }
        else
        {
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
            Assert.Empty(manager.Entries);
        }
    }

    [Fact]
    public async Task Create_RefusesADataConnection_WhileNoEditorCanPickOne()
    {
        // Arrange (review 8, G6): with neither external source feature on, nothing can check the permission.
        var manager = new InMemorySources();
        var body = Body("https://search.vendor.example/", "ApiKey", "plain-key");
        body["Source"] = "AzureAISearch";
        body["DisplayText"] = "Mine";
        body["Properties"]["DataConnectionReference"] = new JsonObject { ["ConnectionId"] = ConnectionId };

        // Act
        var result = await AISourceCatalogApi.CreateAsync(body, Context(), Authorization(), manager, Secrets(), "data-sources", "data source", AIPermissions.ManageAIDataSources, connectionPicker: null);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
        Assert.Empty(manager.Entries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("4qkc7h2m9x1r5t8v3w6y0z2b4e")]
    public async Task Update_RefusesAnAddedOrChangedDataConnection_WithoutManageDataConnections(string storedConnectionId)
    {
        // Arrange (review 8, G6)
        var stored = Stored("https://search.vendor.example/");

        if (storedConnectionId is not null)
        {
            stored.Properties["DataConnectionReference"] = new JsonObject { ["ConnectionId"] = storedConnectionId };
        }

        var manager = Manager(stored);
        var body = new JsonObject { ["Properties"] = new JsonObject { ["DataConnectionReference"] = new JsonObject { ["ConnectionId"] = ConnectionId } } };
        var context = Context();

        // Act
        var result = await AISourceCatalogApi.UpdateAsync(Id, body, context, Authorization(), manager, Secrets(), AIPermissions.ManageAIDataSources, Picker(context, mayManageConnections: false));

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
        Assert.Equal(0, manager.Saves);
    }

    [Fact]
    public async Task Update_KeepsTheStoredDataConnection_WithoutManageDataConnections()
    {
        // Arrange (review 8, G6): as in the editor, someone who may not pick a connection keeps the stored one.
        var stored = Stored("https://search.vendor.example/");
        stored.Properties["DataConnectionReference"] = new JsonObject { ["ConnectionId"] = ConnectionId };
        var manager = Manager(stored);
        var body = new JsonObject { ["Properties"] = new JsonObject { [MetadataName] = new JsonObject { ["IndexName"] = "policies-v2" } } };
        var context = Context();

        // Act
        var result = await AISourceCatalogApi.UpdateAsync(Id, body, context, Authorization(), manager, Secrets(), AIPermissions.ManageAIDataSources, Picker(context, mayManageConnections: false));

        // Assert
        Assert.IsType<Ok<AIDataSource>>(result.Result);
        Assert.Equal(ConnectionId, manager.LastSaved["DataConnectionReference"]["ConnectionId"].GetValue<string>());
    }

    [Fact]
    public void Startups_RegisterThePurposeOfEachSourcesSecrets()
    {
        // Arrange
        var services = new ServiceCollection();
        new CrestApps.OrchardCore.AI.DataSources.AzureAI.Startup(Mock.Of<IStringLocalizer<CrestApps.OrchardCore.AI.DataSources.AzureAI.Startup>>()).ConfigureServices(services);
        new CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Startup(Mock.Of<IStringLocalizer<CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Startup>>()).ConfigureServices(services);
        new CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Startup(Mock.Of<IStringLocalizer<CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Startup>>()).ConfigureServices(services);
        new CrestApps.OrchardCore.AI.FileSources.Ftp.Startup().ConfigureServices(services);
        new CrestApps.OrchardCore.AI.FileSources.Sftp.Startup().ConfigureServices(services);

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IngestionSourceSecretOptions>>().Value;

        // Assert
        Assert.Equal(AIDataSourceProtectionConstants.SourceSecretPurpose, options.GetPurpose(typeof(AIDataSource), "AzureAISearchSourceMetadata"));
        Assert.Equal(AIDataSourceProtectionConstants.SourceSecretPurpose, options.GetPurpose(typeof(AIDataSource), "ElasticsearchSourceMetadata"));
        Assert.Equal(AIDataSourceProtectionConstants.SourceSecretPurpose, options.GetPurpose(typeof(AIDataSource), "PostgreSQLSourceMetadata"));
        Assert.Equal("FtpResourcePassword", options.GetPurpose(typeof(FileSource), "FtpConnectionMetadata"));
        Assert.Equal("SftpResourceCredentials", options.GetPurpose(typeof(FileSource), "SftpConnectionMetadata"));
    }

    private string Protected(string key)
        => _protection.CreateProtector(AIDataSourceProtectionConstants.SourceSecretPurpose).Protect(key);

    private string Unprotect(string key)
        => _protection.CreateProtector(AIDataSourceProtectionConstants.SourceSecretPurpose).Unprotect(key);

    private AISourceSecrets Secrets()
        => new(_protection, SecretOptions());

    private static IngestionSourceSecretOptions SecretOptions()
        => new IngestionSourceSecretOptions().Protect<AIDataSource>(MetadataName, AIDataSourceProtectionConstants.SourceSecretPurpose);

    // The record as the API answers it.
    private static string Serialized(AIDataSource dataSource)
        => JsonSerializer.Serialize(dataSource, JsonSerializerOptions.Web);

    // The answer as it is written to the response, with the API's JSON options.
    private static async Task<(int StatusCode, string Body)> ResponseAsync(IResult result)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        using var body = new MemoryStream();
        context.Response.Body = body;

        await result.ExecuteAsync(context);

        return (context.Response.StatusCode, Encoding.UTF8.GetString(body.ToArray()));
    }

    // The editors' connection picker, for a caller who may or may not manage data connections.
    private static AIDataSourceConnectionPicker Picker(HttpContext context, bool mayManageConnections)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                mayManageConnections || !requirements.OfType<PermissionRequirement>().Any(requirement => requirement.Permission.Name == DataConnectorPermissions.ManageDataConnections.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return new AIDataSourceConnectionPicker(authorization.Object, new HttpContextAccessor { HttpContext = context }, new PassThroughStringLocalizer<AIDataSourceConnectionPicker>(), Mock.Of<IDataConnectionResolver>());
    }

    private static AIDataSource Stored(string endpoint, string key = StoredKey)
    {
        var dataSource = new AIDataSource { ItemId = Id, Source = "AzureAISearch", DisplayText = "Policies" };
        dataSource.Properties[MetadataName] = new JsonObject
        {
            ["Endpoint"] = endpoint,
            ["AuthenticationType"] = "ApiKey",
            ["ApiKey"] = key,
            ["IndexName"] = "policies",
        };

        return dataSource;
    }

    // The body a caller sends after reading the record: its properties, with the protected key the read returned.
    private static JsonObject Body(string endpoint, string authenticationType, string key)
        => new()
        {
            ["Properties"] = new JsonObject
            {
                [MetadataName] = new JsonObject
                {
                    ["Endpoint"] = endpoint,
                    ["AuthenticationType"] = authenticationType,
                    ["ApiKey"] = key,
                    ["IndexName"] = "policies",
                },
            },
        };

    private static JsonObject Metadata(AIDataSource dataSource)
        => JsonSerializer.SerializeToNode(dataSource.Properties[MetadataName], CatalogDeploymentSerializer.Options).AsObject();

    private static InMemorySources Manager(AIDataSource stored)
    {
        var manager = new InMemorySources();
        manager.Entries.Add(stored);

        return manager;
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

    /// <summary>
    /// A data source manager kept in memory that applies JSON as the catalog handler does (the posted properties replace
    /// the stored ones) and records what each save wrote, as a store that writes the record when it is saved.
    /// </summary>
    private sealed class InMemorySources : ISourceCatalogManager<AIDataSource>
    {
        public List<AIDataSource> Entries { get; } = [];

        public bool RequireKey { get; set; }

        public int Saves { get; private set; }

        public JsonObject LastSaved { get; private set; }

        public ValueTask<AIDataSource> FindByIdAsync(string id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Entries.FirstOrDefault(entry => entry.ItemId == id));

        public ValueTask<IEnumerable<AIDataSource>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IEnumerable<AIDataSource>>(Entries.ToList());

        public ValueTask<PageResult<AIDataSource>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
            where TQuery : QueryContext
            => throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(AIDataSource model, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Entries.Remove(model));

        public ValueTask CreateAsync(AIDataSource model, CancellationToken cancellationToken = default)
        {
            Entries.Add(model);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(AIDataSource model, JsonNode data, CancellationToken cancellationToken = default)
        {
            CatalogDeploymentSerializer.Populate(model, data);
            Saves++;
            LastSaved = JsonSerializer.SerializeToNode(model.Properties, CatalogDeploymentSerializer.Options).AsObject();

            return ValueTask.CompletedTask;
        }

        public ValueTask<ValidationResultDetails> ValidateAsync(AIDataSource model, CancellationToken cancellationToken = default)
        {
            var result = new ValidationResultDetails();
            var metadata = JsonSerializer.SerializeToNode(model.Properties, CatalogDeploymentSerializer.Options)?[MetadataName];

            if (RequireKey && string.IsNullOrEmpty(metadata?["ApiKey"]?.GetValue<string>()))
            {
                result.Fail(new ValidationResult("API key is required.", ["ApiKey"]));
            }

            return ValueTask.FromResult(result);
        }

        public ValueTask<AIDataSource> NewAsync(string source, JsonNode data, CancellationToken cancellationToken = default)
        {
            var entry = new AIDataSource { Source = source };

            CatalogDeploymentSerializer.Populate(entry, data);
            entry.Source = source;

            return ValueTask.FromResult(entry);
        }

        public ValueTask<IEnumerable<AIDataSource>> GetAsync(string source, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IEnumerable<AIDataSource>>(Entries.Where(entry => entry.Source == source).ToList());

        public ValueTask<IEnumerable<AIDataSource>> FindBySourceAsync(string source, CancellationToken cancellationToken = default)
            => GetAsync(source, cancellationToken);
    }
}
