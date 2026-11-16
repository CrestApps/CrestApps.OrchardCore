using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.Core.Deployments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

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

    [Fact]
    public async Task Update_DropsTheKey_WhenTheBodySendsTheProtectedKeyBackWithAnotherEndpoint()
    {
        // Arrange
        var manager = Manager(Stored("https://search.vendor.example/"));
        var body = Body("https://attacker.example/", "ApiKey", StoredKey);

        // Act
        var result = await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, AIPermissions.ManageAIDataSources);

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
        await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, AIPermissions.ManageAIDataSources);

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
        var result = await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, AIPermissions.ManageAIDataSources);

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
        await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, AIPermissions.ManageAIDataSources);

        // Assert
        Assert.Equal("new-key", manager.LastSaved[MetadataName]["ApiKey"].GetValue<string>());
    }

    [Fact]
    public async Task Update_ReportsARequiredKey_ThatTheMoveDropped_AndSavesNothing()
    {
        // Arrange
        var manager = Manager(Stored("https://search.vendor.example/"));
        manager.RequireKey = true;
        var body = Body("https://attacker.example/", "ApiKey", StoredKey);

        // Act
        var result = await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, AIPermissions.ManageAIDataSources);

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.True(problem.ProblemDetails.Errors.ContainsKey("ApiKey"));
        Assert.Equal(0, manager.Saves);
        Assert.Equal(StoredKey, Metadata(manager.Entries.Single())["ApiKey"].GetValue<string>());
    }

    private static AIDataSource Stored(string endpoint)
    {
        var dataSource = new AIDataSource { ItemId = Id, Source = "AzureAISearch", DisplayText = "Policies" };
        dataSource.Properties[MetadataName] = new JsonObject
        {
            ["Endpoint"] = endpoint,
            ["AuthenticationType"] = "ApiKey",
            ["ApiKey"] = StoredKey,
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
