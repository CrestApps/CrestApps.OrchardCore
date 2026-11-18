using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.Handlers;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;
using CrestApps.OrchardCore.AI.DataSources.Services;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// Review 9, K1: a records-API PUT that moved an Elasticsearch data source saved the record with the new URL and the kept
/// password first, and dropped the password in a second save. The catalog queues a sync of the record on every update,
/// so the first save had already handed the new URL and the old password to the deferred sync.
/// </summary>
public sealed class ElasticsearchSourceMoveQueuesNoKeptSecretTests
{
    private const string Id = "4ds00000000000000000000009";
    private const string MetadataName = nameof(ElasticsearchSourceMetadata);
    private const string StoredUrl = "https://search.vendor.example:9200";
    private const string NewUrl = "https://attacker.example";
    private const string Password = "the-stored-password";

    private readonly EphemeralDataProtectionProvider _protection = new();

    [Fact]
    public async Task Update_ThatMovesTheSource_WithABlankUsername_NeverSavesOrQueuesTheNewUrlWithTheStoredPassword()
    {
        // Arrange: the body Jack sent, from a caller who never saw the password.
        var site = new Site(_protection, Stored());
        var body = Body("\"Url\":\"" + NewUrl + "\",\"Username\":\"\"");

        // Act
        await site.UpdateAsync(body);

        // Assert
        Assert.DoesNotContain(site.Saved, record => SendsThePasswordTo(record, NewUrl));
        Assert.DoesNotContain(site.Queue.Synced, record => SendsThePasswordTo(record, NewUrl));
    }

    [Fact]
    public async Task Update_ThatMovesTheSource_AndStopsSigningIn_SavesOnce_WithoutTheKeptPassword()
    {
        // Arrange: a move whose dropped copy is still valid, so the update is saved.
        var site = new Site(_protection, Stored());
        var body = Body("\"Url\":\"" + NewUrl + "\",\"AuthenticationType\":\"None\"");

        // Act
        var result = await site.UpdateAsync(body);

        // Assert
        Assert.IsType<Ok<AIDataSource>>(result.Result);
        var saved = Assert.Single(site.Saved);
        Assert.Equal(NewUrl, Metadata(saved)["Url"].GetValue<string>());
        Assert.Null(Metadata(saved)["Password"]);
        var synced = Assert.Single(site.Queue.Synced);
        Assert.Null(Metadata(synced)["Password"]);
    }

    [Fact]
    public async Task Update_ThatKeepsTheUrl_KeepsThePassword()
    {
        // Arrange
        var site = new Site(_protection, Stored());
        var body = Body("\"IndexName\":\"logs-v2\"");

        // Act
        var result = await site.UpdateAsync(body);

        // Assert
        Assert.IsType<Ok<AIDataSource>>(result.Result);
        var saved = Assert.Single(site.Saved);
        Assert.Equal(Password, _protection.UnprotectSetting(AIDataSourceProtectionConstants.SourceSecretPurpose, Metadata(saved)["Password"].GetValue<string>()));
    }

    [Fact]
    public async Task DeferredSync_ReadsTheDataSourceAgain_RatherThanTheCopyItQueued()
    {
        // Arrange: the copy queued by an earlier save of the request, and the record as the request left it.
        var queued = Stored();
        var stored = Stored();
        stored.Put(new ElasticsearchSourceMetadata { EnvironmentType = ElasticsearchSourceMetadata.SelfManagedEnvironmentType, Url = NewUrl, AuthenticationType = ElasticsearchSourceMetadata.NoneAuthenticationType, IndexName = "logs" });
        var deleted = new AIDataSource { ItemId = "4ds00000000000000000000010", Source = AIDataSourceSourceTypes.Elasticsearch };

        var store = new Mock<IAIDataSourceStore>();
        store.Setup(x => x.FindByIdAsync(Id, It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(stored));
        var synced = new List<AIDataSource>();
        var indexing = new Mock<IAIDataSourceIndexingService>();
        indexing
            .Setup(x => x.SyncDataSourceAsync(It.IsAny<AIDataSource>(), It.IsAny<CancellationToken>()))
            .Callback((AIDataSource dataSource, CancellationToken _) => synced.Add(dataSource))
            .Returns(Task.CompletedTask);

        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(store.Object)
            .AddSingleton(indexing.Object)
            .BuildServiceProvider();

        // Act
        await OrchardAIDataSourceIndexingQueue.ProcessAsync(services, [
            OrchardAIDataSourceIndexingWorkItem.ForSyncDataSource(queued),
            OrchardAIDataSourceIndexingWorkItem.ForSyncDataSource(deleted),
        ]);

        // Assert: the stored record is synced; one no longer stored is not.
        var dataSource = Assert.Single(synced);
        Assert.Same(stored, dataSource);
        Assert.Null(Metadata(Serialize(dataSource))["Password"]);
    }

    private AIDataSource Stored()
    {
        var dataSource = new AIDataSource
        {
            ItemId = Id,
            Source = AIDataSourceSourceTypes.Elasticsearch,
            DisplayText = "Logs",
            AIKnowledgeBaseIndexProfileName = "knowledge",
            ContentFieldName = "message",
        };
        dataSource.Put(new ElasticsearchSourceMetadata
        {
            EnvironmentType = ElasticsearchSourceMetadata.SelfManagedEnvironmentType,
            Url = StoredUrl,
            AuthenticationType = ElasticsearchSourceMetadata.BasicAuthenticationType,
            Username = "elastic",
            Password = _protection.ProtectSetting(AIDataSourceProtectionConstants.SourceSecretPurpose, Password),
            IndexName = "logs",
        });

        return dataSource;
    }

    private bool SendsThePasswordTo(JsonObject record, string url)
    {
        var metadata = Metadata(record);

        return string.Equals(metadata["Url"]?.GetValue<string>(), url, StringComparison.Ordinal) &&
            metadata["Password"]?.GetValue<string>() is { Length: > 0 } password &&
            _protection.UnprotectSetting(AIDataSourceProtectionConstants.SourceSecretPurpose, password) == Password;
    }

    // A PUT body that changes the given members of the Elasticsearch settings.
    private static JsonObject Body(string members)
        => JsonNode.Parse("{\"Properties\":{\"" + MetadataName + "\":{" + members + "}}}").AsObject();

    private static JsonObject Metadata(JsonObject record)
        => record["Properties"]?[MetadataName]?.AsObject() ?? [];

    private static JsonObject Serialize(AIDataSource dataSource)
        => JsonSerializer.SerializeToNode(dataSource).AsObject();

    /// <summary>
    /// The records API over the data source manager the site uses, with the catalog handlers the modules register:
    /// CrestApps.Core's, which queues a sync on every update, the Elasticsearch source handler and the copy of what was
    /// stored. The store returns the instance it keeps, as a session does, and records what each save wrote.
    /// </summary>
    private sealed class Site
    {
        private readonly EphemeralDataProtectionProvider _protection;
        private readonly AIDataSource _stored;

        public Site(EphemeralDataProtectionProvider protection, AIDataSource stored)
        {
            _protection = protection;
            _stored = stored;
        }

        public RecordingQueue Queue { get; } = new();

        public List<JsonObject> Saved { get; } = [];

        public async Task<Results<Ok<AIDataSource>, ValidationProblem, ProblemHttpResult>> UpdateAsync(JsonObject body)
        {
            var store = new Mock<IAIDataSourceStore>();
            store.Setup(x => x.FindByIdAsync(Id, It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(_stored));
            store
                .Setup(x => x.UpdateAsync(It.IsAny<AIDataSource>(), It.IsAny<CancellationToken>()))
                .Callback((AIDataSource dataSource, CancellationToken _) => Saved.Add(Serialize(dataSource)))
                .Returns(ValueTask.CompletedTask);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.AddSingleton<IDataProtectionProvider>(_protection);
            services.AddSingleton(Options.Create(new ElasticsearchDataSourceOptions()));
            services.AddSingleton(Mock.Of<IAIEndpointPolicy>(policy => policy.IsAllowed(It.IsAny<string>()) == true));
            services.AddSingleton<IAIDataSourceIndexingQueue>(Queue);
            services.AddCoreAIDataSourceRag();
            services.AddAIDataSourceSourceHandler<ElasticsearchAIDataSourceSourceHandler>(AIDataSourceSourceTypes.Elasticsearch);
            services.AddScoped<ICatalogEntryHandler<AIDataSource>, AIDataSourceSourceValidationHandler>();

            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var manager = new DefaultAIDataSourceManager(store.Object, scope.ServiceProvider.GetServices<ICatalogEntryHandler<AIDataSource>>(), NullLogger<DefaultAIDataSourceManager>.Instance);
            var secrets = new AISourceSecrets(_protection, new IngestionSourceSecretOptions().Protect<AIDataSource>(MetadataName, AIDataSourceProtectionConstants.SourceSecretPurpose));

            return await AISourceCatalogApi.UpdateAsync(Id, body, Context(), Authorization(), manager, secrets, AIPermissions.ManageAIDataSources);
        }

        private static DefaultHttpContext Context()
            => new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "manager")], "Test")) };

        private static IAuthorizationService Authorization()
        {
            var authorization = new Mock<IAuthorizationService>();
            authorization
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Success());

            return authorization.Object;
        }
    }

    /// <summary>
    /// Records a copy of every data source queued for a sync, as the site's queue keeps a clone of it.
    /// </summary>
    private sealed class RecordingQueue : IAIDataSourceIndexingQueue
    {
        public List<JsonObject> Synced { get; } = [];

        public ValueTask QueueSyncDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
        {
            Synced.Add(Serialize(dataSource));

            return ValueTask.CompletedTask;
        }

        public ValueTask QueueDeleteDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask QueueSyncSourceDocumentsAsync(string sourceIndexProfileName, IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask QueueRemoveSourceDocumentsAsync(string sourceIndexProfileName, IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask QueueSyncDataSourceDocumentsAsync(string dataSourceId, IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask QueueRemoveDataSourceDocumentsAsync(string dataSourceId, IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
