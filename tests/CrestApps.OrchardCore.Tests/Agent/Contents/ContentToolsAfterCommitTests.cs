using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents.Services;
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.FileStorage;
using OrchardCore.Json;
using OrchardCore.Media;
using OrchardCore.Media.Fields;
using OrchardCore.Media.Settings;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using Usr = OrchardCore.Users;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

/// <summary>
/// Runs the content tools through a real YesSql store and content manager, because the defect this pins is invisible
/// to a mocked content manager: a tool that generates a file commits the session mid-turn, which ends YesSql's tracking
/// of every item but leaves them in the content manager's cache. A later tool that loads and saves one of those items
/// from the cache throws "An object with the same identity is already part of this transaction".
/// </summary>
public sealed class ContentToolsAfterCommitTests
{
    private const string ContentType = "Article";
    private const string PageContentType = "Page";
    private const string MediaContentType = "KnowledgeBaseArticle";
    private const string UnversionedContentType = "UnversionedArticle";
    private const string MediaFieldName = "Documents";
    private const string InteractionId = "interaction-1";

    [Fact]
    public async Task CreateOrUpdate_UpdatesAPublishedItemSearchedEarlierInTheTurn()
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: true);

        await using (var turn = site.StartTurn())
        {
            await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Original" });
            await turn.CommitAsync();

            var result = await turn.InvokeAsync(new CreateOrUpdateContentTool(), UpdateArguments(contentItemId, "Updated"));

            Assert.Contains("successfully published", Assert.IsType<string>(result), StringComparison.Ordinal);
            await turn.CommitAsync();
        }

        var versions = await site.GetVersionsAsync(contentItemId);

        Assert.Equal(2, versions.Count);
        Assert.Equal("Updated", TestSite.GetTitle(Assert.Single(versions, x => x.Latest && x.Published)));
    }

    [Fact]
    public async Task CreateOrUpdate_UpdatesADraftSearchedEarlierInTheTurn()
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: false);

        await using (var turn = site.StartTurn())
        {
            await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Original" });
            await turn.CommitAsync();

            var result = await turn.InvokeAsync(new CreateOrUpdateContentTool(), UpdateArguments(contentItemId, "Updated", isDraft: true));

            Assert.Contains("draft content item", Assert.IsType<string>(result), StringComparison.Ordinal);
            await turn.CommitAsync();
        }

        var version = Assert.Single(await site.GetVersionsAsync(contentItemId));

        Assert.True(version.Latest);
        Assert.False(version.Published);
        Assert.Equal("Updated", TestSite.GetTitle(version));
    }

    [Fact]
    public async Task CreateOrUpdate_UpdatesAnItemReadByIdEarlierInTheTurn()
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: true);

        await using (var turn = site.StartTurn())
        {
            await turn.InvokeAsync(new GetContentTool(), new() { ["contentItemId"] = contentItemId });
            await turn.CommitAsync();

            await turn.InvokeAsync(new CreateOrUpdateContentTool(), UpdateArguments(contentItemId, "Updated"));
            await turn.CommitAsync();
        }

        var versions = await site.GetVersionsAsync(contentItemId);

        Assert.Equal("Updated", TestSite.GetTitle(Assert.Single(versions, x => x.Latest && x.Published)));
    }

    [Fact]
    public async Task CreateOrUpdate_UpdatesAnItemItCreatedEarlierInTheTurn()
    {
        await using var site = await TestSite.CreateAsync();
        string contentItemId;

        await using (var turn = site.StartTurn())
        {
            var created = await turn.InvokeAsync(new CreateOrUpdateContentTool(), new()
            {
                ["contentItem"] = $$"""{ "ContentType": "{{ContentType}}", "TitlePart": { "Title": "Original" } }""",
                ["isDraft"] = true,
            });

            contentItemId = ExtractQuotedId(Assert.IsType<string>(created));
            await turn.CommitAsync();

            await turn.InvokeAsync(new CreateOrUpdateContentTool(), UpdateArguments(contentItemId, "Updated", isDraft: true));
            await turn.CommitAsync();
        }

        Assert.Equal("Updated", TestSite.GetTitle(Assert.Single(await site.GetVersionsAsync(contentItemId))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateOrUpdate_LoadsANestedItemFromTheDatabaseAfterACommit(bool nestedPayloadHasContentType)
    {
        await using var site = await TestSite.CreateAsync();
        var nestedContentItemId = await site.SeedAsync("Nested", published: true);
        var nestedContentType = nestedPayloadHasContentType ? $$""" "ContentType": "{{ContentType}}", """ : string.Empty;

        await using (var turn = site.StartTurn())
        {
            await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Nested" });
            await turn.CommitAsync();

            var result = await turn.InvokeAsync(new CreateOrUpdateContentTool(), new()
            {
                ["contentItem"] = $$"""
                {
                  "ContentType": "{{PageContentType}}",
                  "BagPart": { "ContentItems": [ { {{nestedContentType}} "ContentItemId": "{{nestedContentItemId}}" } ] }
                }
                """,
            });

            Assert.Contains("successfully published", Assert.IsType<string>(result), StringComparison.Ordinal);
            await turn.CommitAsync();
        }
    }

    [Fact]
    public async Task CreateOrUpdate_StillUpdatesTwiceWithoutACommitInBetween()
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: true);

        await using (var turn = site.StartTurn())
        {
            await turn.InvokeAsync(new CreateOrUpdateContentTool(), UpdateArguments(contentItemId, "First"));
            await turn.InvokeAsync(new CreateOrUpdateContentTool(), UpdateArguments(contentItemId, "Second"));
            await turn.CommitAsync();
        }

        var versions = await site.GetVersionsAsync(contentItemId);

        Assert.Equal("Second", TestSite.GetTitle(Assert.Single(versions, x => x.Latest && x.Published)));
    }

    [Fact]
    public async Task Publish_PublishesADraftSearchedEarlierInTheTurn()
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: false);

        await using (var turn = site.StartTurn())
        {
            await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Original" });
            await turn.CommitAsync();

            var result = await turn.InvokeAsync(new PublishContentTool(), new() { ["contentItemId"] = contentItemId });

            Assert.Equal("Content item was successfully published", result);
            await turn.CommitAsync();
        }

        var version = Assert.Single(await site.GetVersionsAsync(contentItemId));

        Assert.True(version.Published);
    }

    [Fact]
    public async Task Publish_PublishesADraftCreatedEarlierInTheTurn()
    {
        await using var site = await TestSite.CreateAsync();
        string contentItemId;

        await using (var turn = site.StartTurn())
        {
            var created = await turn.InvokeAsync(new CreateOrUpdateContentTool(), new()
            {
                ["contentItem"] = $$"""{ "ContentType": "{{ContentType}}", "TitlePart": { "Title": "Original" } }""",
                ["isDraft"] = true,
            });

            contentItemId = ExtractQuotedId(Assert.IsType<string>(created));
            await turn.CommitAsync();

            var result = await turn.InvokeAsync(new PublishContentTool(), new() { ["contentItemId"] = contentItemId });

            Assert.Equal("Content item was successfully published", result);
            await turn.CommitAsync();
        }

        Assert.True(Assert.Single(await site.GetVersionsAsync(contentItemId)).Published);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unpublish_UnpublishesAnItemSearchedEarlierInTheTurn(bool searchedAgainAfterTheCommit)
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: true);

        await using (var turn = site.StartTurn())
        {
            await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Original" });
            await turn.CommitAsync();

            if (searchedAgainAfterTheCommit)
            {
                await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Original" });
            }

            var result = await turn.InvokeAsync(new UnpublishContentTool(), new() { ["contentItemId"] = contentItemId });

            Assert.Equal("Content item was successfully unpublished", result);
            await turn.CommitAsync();
        }

        Assert.False(Assert.Single(await site.GetVersionsAsync(contentItemId)).Published);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_IsSafeForAnItemSearchedEarlierInTheTurn(bool searchedAgainAfterTheCommit)
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: true);

        await using (var turn = site.StartTurn())
        {
            await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Original" });
            await turn.CommitAsync();

            if (searchedAgainAfterTheCommit)
            {
                await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Original" });
            }

            var result = await turn.InvokeAsync(new DeleteContentTool(), new() { ["contentItemId"] = contentItemId });

            Assert.Equal("Content item was successfully deleted", result);
            await turn.CommitAsync();
        }

        var version = Assert.Single(await site.GetVersionsAsync(contentItemId));

        Assert.False(version.Published);
        Assert.False(version.Latest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clone_IsSafeForAnItemSearchedEarlierInTheTurn(bool searchedAgainAfterTheCommit)
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: true);

        await using (var turn = site.StartTurn())
        {
            await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Original" });
            await turn.CommitAsync();

            if (searchedAgainAfterTheCommit)
            {
                await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Original" });
            }

            var result = await turn.InvokeAsync(new CloneContentTool(), new() { ["contentItemId"] = contentItemId });

            Assert.StartsWith("Content item was successfully cloned", Assert.IsType<string>(result), StringComparison.Ordinal);
            await turn.CommitAsync();
        }

        Assert.True(Assert.Single(await site.GetVersionsAsync(contentItemId)).Published);
    }

    [Fact]
    public async Task Get_IsSafeForAnItemSearchedEarlierInTheTurn()
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: true);

        await using var turn = site.StartTurn();

        await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Original" });
        await turn.CommitAsync();

        var result = await turn.InvokeAsync(new GetContentTool(), new() { ["contentItemId"] = contentItemId });

        Assert.Contains(contentItemId, Assert.IsType<string>(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Attach_AttachesToAPublishedItemSearchedEarlierInTheTurn()
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: true, MediaContentType);

        await using (var turn = site.StartTurn())
        {
            using var scope = AIInvocationScope.Begin();

            AIInvocationScope.Current.ToolExecutionContext = new AIToolExecutionContext(new ChatInteraction
            {
                ItemId = InteractionId,
            });

            await turn.InvokeAsync(new SearchForContentsTool(), new() { ["term"] = "Original" });
            await turn.CommitAsync();

            var result = await turn.InvokeAsync(new AttachDocumentToContentItemTool(), new() { ["contentItemId"] = contentItemId });

            Assert.Contains("was attached", Assert.IsType<string>(result), StringComparison.Ordinal);
            await turn.CommitAsync();
        }

        var versions = await site.GetVersionsAsync(contentItemId);

        Assert.Equal(2, versions.Count);
        Assert.Equal(
            [$"mediafields/{MediaContentType}/{contentItemId}/guide.pdf"],
            TestSite.GetMediaPaths(Assert.Single(versions, x => x.Latest && x.Published)));
    }

    [Fact]
    public async Task Attach_KeepsAPublishedItemWithoutVersionsPublished()
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: true, UnversionedContentType);

        await using (var turn = site.StartTurn())
        {
            using var scope = AIInvocationScope.Begin();

            AIInvocationScope.Current.ToolExecutionContext = new AIToolExecutionContext(new ChatInteraction
            {
                ItemId = InteractionId,
            });

            var result = await turn.InvokeAsync(new AttachDocumentToContentItemTool(), new() { ["contentItemId"] = contentItemId });

            Assert.Contains("which was published", Assert.IsType<string>(result), StringComparison.Ordinal);
            await turn.CommitAsync();
        }

        var version = Assert.Single(await site.GetVersionsAsync(contentItemId));

        Assert.True(version.Published);
        Assert.Equal([$"mediafields/{UnversionedContentType}/{contentItemId}/guide.pdf"], TestSite.GetMediaPaths(version));
    }

    [Fact]
    public async Task Attach_KeepsAPublishedItemWithoutVersionsPublishedWhenItIsNotValid()
    {
        await using var site = await TestSite.CreateAsync();
        var contentItemId = await site.SeedAsync("Original", published: true, UnversionedContentType);

        await using (var turn = site.StartTurn(new FailingValidationHandler()))
        {
            using var scope = AIInvocationScope.Begin();

            AIInvocationScope.Current.ToolExecutionContext = new AIToolExecutionContext(new ChatInteraction
            {
                ItemId = InteractionId,
            });

            var result = await turn.InvokeAsync(new AttachDocumentToContentItemTool(), new() { ["contentItemId"] = contentItemId });

            Assert.Contains("not valid", Assert.IsType<string>(result), StringComparison.Ordinal);
            await turn.CommitAsync();
        }

        Assert.True(Assert.Single(await site.GetVersionsAsync(contentItemId)).Published);
    }

    private static Dictionary<string, object> UpdateArguments(string contentItemId, string title, bool isDraft = false) => new()
    {
        ["contentItem"] = $$"""
        {
          "ContentItemId": "{{contentItemId}}",
          "ContentType": "{{ContentType}}",
          "TitlePart": { "Title": "{{title}}" }
        }
        """,
        ["isDraft"] = isDraft,
    };

    private static string ExtractQuotedId(string response)
    {
        var start = response.IndexOf('\'', StringComparison.Ordinal) + 1;

        return response[start..response.IndexOf('\'', start)];
    }

    private sealed class TestSite : IAsyncDisposable
    {
        private static readonly DocumentJsonSerializerOptions _jsonOptions = new();

        private readonly IStore _store;
        private readonly string _databasePath;

        private TestSite(IStore store, string databasePath)
        {
            _store = store;
            _databasePath = databasePath;
        }

        public static async Task<TestSite> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"crestapps-content-tools-{Guid.NewGuid():N}.db");
            var store = await StoreFactory.CreateAndInitializeAsync(configuration => configuration.UseSqLite($"Data Source={path};Pooling=False"));

            store.RegisterIndexes([new ContentItemIndexProvider()]);

            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

            await schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
                .Column<string>(nameof(ContentItemIndex.ContentItemId), column => column.WithLength(26))
                .Column<string>(nameof(ContentItemIndex.ContentItemVersionId), column => column.WithLength(26))
                .Column<bool>(nameof(ContentItemIndex.Latest))
                .Column<bool>(nameof(ContentItemIndex.Published))
                .Column<string>(nameof(ContentItemIndex.ContentType), column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
                .Column<DateTime>(nameof(ContentItemIndex.ModifiedUtc), column => column.Nullable())
                .Column<DateTime>(nameof(ContentItemIndex.PublishedUtc), column => column.Nullable())
                .Column<DateTime>(nameof(ContentItemIndex.CreatedUtc), column => column.Nullable())
                .Column<string>(nameof(ContentItemIndex.Owner), column => column.Nullable().WithLength(ContentItemIndex.MaxOwnerSize))
                .Column<string>(nameof(ContentItemIndex.Author), column => column.Nullable().WithLength(ContentItemIndex.MaxAuthorSize))
                .Column<string>(nameof(ContentItemIndex.DisplayText), column => column.Nullable().WithLength(ContentItemIndex.MaxDisplayTextSize)));

            await transaction.CommitAsync(TestContext.Current.CancellationToken);

            return new TestSite(store, path);
        }

        public async Task<string> SeedAsync(string title, bool published, string contentType = ContentType)
        {
            await using var turn = StartTurn();

            var contentManager = turn.Services.GetRequiredService<IContentManager>();
            var contentItem = await contentManager.NewAsync(contentType);

            contentItem.Merge(new { TitlePart = new { Title = title } });

            await contentManager.CreateAsync(contentItem, published ? VersionOptions.Published : VersionOptions.Draft);
            await turn.CommitAsync();

            return contentItem.ContentItemId;
        }

        public async Task<IReadOnlyList<ContentItem>> GetVersionsAsync(string contentItemId)
        {
            await using var session = _store.CreateSession();

            var versions = await session
                .Query<ContentItem, ContentItemIndex>(x => x.ContentItemId == contentItemId)
                .ListAsync(TestContext.Current.CancellationToken);

            return versions.ToList();
        }

        public static string GetTitle(ContentItem contentItem) =>
            JsonSerializer.SerializeToNode(contentItem, _jsonOptions.SerializerOptions)["TitlePart"]["Title"].GetValue<string>();

        public static string[] GetMediaPaths(ContentItem contentItem) =>
            JsonSerializer.SerializeToNode(contentItem, _jsonOptions.SerializerOptions)[MediaContentType][MediaFieldName]["Paths"].Deserialize<string[]>();

        public Turn StartTurn(IContentHandler handler = null)
        {
            var contentDefinitionManager = new Mock<IContentDefinitionManager>();
            var settings = new JsonObject
            {
                [nameof(ContentTypeSettings)] = new JsonObject
                {
                    [nameof(ContentTypeSettings.Versionable)] = true,
                },
            };

            contentDefinitionManager
                .Setup(x => x.GetTypeDefinitionAsync(ContentType))
                .ReturnsAsync(new ContentTypeDefinition(ContentType, ContentType, [], settings));
            contentDefinitionManager
                .Setup(x => x.GetTypeDefinitionAsync(PageContentType))
                .ReturnsAsync(new ContentTypeDefinition(PageContentType, PageContentType,
                [
                    new ContentTypePartDefinition("BagPart", new ContentPartDefinition("BagPart", [], []), []),
                ], settings));

            contentDefinitionManager
                .Setup(x => x.GetTypeDefinitionAsync(MediaContentType))
                .ReturnsAsync(new ContentTypeDefinition(MediaContentType, MediaContentType,
                [
                    new ContentTypePartDefinition(MediaContentType, new ContentPartDefinition(MediaContentType,
                    [
                        new ContentPartFieldDefinition(new ContentFieldDefinition(nameof(MediaField)), MediaFieldName, new JsonObject
                        {
                            [nameof(MediaFieldSettings)] = JsonSerializer.SerializeToNode(new MediaFieldSettings { Multiple = true }),
                            [nameof(ContentPartFieldSettings)] = JsonSerializer.SerializeToNode(new ContentPartFieldSettings()),
                        }),
                    ], []), []),
                ], settings));

            contentDefinitionManager
                .Setup(x => x.GetTypeDefinitionAsync(UnversionedContentType))
                .ReturnsAsync(new ContentTypeDefinition(UnversionedContentType, UnversionedContentType,
                [
                    new ContentTypePartDefinition(MediaContentType, new ContentPartDefinition(MediaContentType,
                    [
                        new ContentPartFieldDefinition(new ContentFieldDefinition(nameof(MediaField)), MediaFieldName, new JsonObject
                        {
                            [nameof(MediaFieldSettings)] = JsonSerializer.SerializeToNode(new MediaFieldSettings { Multiple = true }),
                            [nameof(ContentPartFieldSettings)] = JsonSerializer.SerializeToNode(new ContentPartFieldSettings()),
                        }),
                    ], []), []),
                ], new JsonObject
                {
                    [nameof(ContentTypeSettings)] = new JsonObject
                    {
                        [nameof(ContentTypeSettings.Versionable)] = false,
                    },
                }));

            var documentStore = new Mock<IAIDocumentStore>();

            documentStore
                .Setup(x => x.GetDocumentsAsync(InteractionId, AIConstants.DocumentReferenceTypes.ChatInteraction))
                .ReturnsAsync(
                [
                    new AIDocument
                    {
                        ItemId = "document-1",
                        FileName = "guide.pdf",
                        StoredFilePath = $"chat-interaction/{InteractionId}/guide.pdf",
                        FileSize = 3,
                    },
                ]);

            var documentFileStore = new Mock<IDocumentFileStore>();

            documentFileStore
                .Setup(x => x.GetFileAsync(It.IsAny<string>()))
                .ReturnsAsync(() => new MemoryStream([1, 2, 3]));

            var mediaFileStore = new Mock<IMediaFileStore>();

            mediaFileStore
                .Setup(x => x.CreateFileFromStreamAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                .ReturnsAsync((string path, Stream _, bool _) => path);

            var idGenerator = new Mock<IContentItemIdGenerator>();

            idGenerator.Setup(x => x.GenerateUniqueId(It.IsAny<ContentItem>())).Returns(() => Guid.NewGuid().ToString("N")[..26]);

            var clock = new Mock<IClock>();

            clock.SetupGet(x => x.UtcNow).Returns(DateTime.UtcNow);

            var listQueryService = new Mock<IContentsAdminListQueryService>();
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddOptions();
            services.AddSingleton(Options.Create(_jsonOptions));
            services.AddSingleton(Options.Create(new PagerOptions()));
            services.AddSingleton(_ => _store.CreateSession());
            services.AddSingleton<IContentManagerSession, DefaultContentManagerSession>();
            services.AddSingleton<IEnumerable<IContentHandler>>(handler is null ? [] : [handler]);
            services.AddSingleton(contentDefinitionManager.Object);
            services.AddSingleton(idGenerator.Object);
            services.AddSingleton(clock.Object);
            services.AddSingleton(Mock.Of<IUpdateModelAccessor>());
            services.AddSingleton(Mock.Of<IStringLocalizer<DefaultContentManager>>());
            services.AddSingleton(Mock.Of<IHttpContextAccessor>());
            services.AddSingleton(new Mock<UserManager<Usr.IUser>>(Mock.Of<IUserStore<Usr.IUser>>(), null, null, null, null, null, null, null, null).Object);
            services.AddSingleton<IContentManager, DefaultContentManager>();
            services.AddSingleton<IStoreCommitter, ContentManagerStoreCommitter>();
            services.AddSingleton(documentStore.Object);
            services.AddSingleton(documentFileStore.Object);
            services.AddSingleton(mediaFileStore.Object);
            services.AddSingleton(new FileCreationService([]));
            services.AddSingleton(Options.Create(new MediaOptions
            {
                AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf" },
            }));
            services.AddSingleton<ContentItemPreparationService>();
            services.AddSingleton<IEnumerable<IContentSchemaDefinition>>([]);
            services.AddSingleton<IContentItemPayloadAssistanceService, AcceptAllPayloadAssistanceService>();
            services.AddSingleton(provider =>
            {
                var session = provider.GetRequiredService<ISession>();

                listQueryService
                    .Setup(x => x.QueryAsync(It.IsAny<ContentOptionsViewModel>(), It.IsAny<IUpdateModel>()))
                    .ReturnsAsync(() => session.Query<ContentItem, ContentItemIndex>(x => x.Latest));

                return listQueryService.Object;
            });

            return new Turn(services.BuildServiceProvider());
        }

        public ValueTask DisposeAsync()
        {
            TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// One AI turn: every tool shares the turn's YesSql session and content manager cache, as they share a request scope in a site.
    /// </summary>
    private sealed class Turn(ServiceProvider services) : IAsyncDisposable
    {
        public IServiceProvider Services => services;

        public async Task<object> InvokeAsync(AIFunction tool, Dictionary<string, object> arguments) =>
            await tool.InvokeAsync(new AIFunctionArguments(arguments) { Services = services }, TestContext.Current.CancellationToken);

        /// <summary>
        /// Commits the way a tool that generates a file does, through the store committer the site registers.
        /// </summary>
        public Task CommitAsync() =>
            services.GetRequiredService<IStoreCommitter>().CommitAsync(TestContext.Current.CancellationToken).AsTask();

        public ValueTask DisposeAsync() => services.DisposeAsync();
    }

    private sealed class FailingValidationHandler : ContentHandlerBase
    {
        public override Task ValidatingAsync(ValidateContentContext context)
        {
            context.ContentValidateResult.Fail(new System.ComponentModel.DataAnnotations.ValidationResult("The title is required."));

            return Task.CompletedTask;
        }
    }

    private sealed class AcceptAllPayloadAssistanceService : IContentItemPayloadAssistanceService
    {
        public ValueTask<ContentItemPayloadValidationResult> ValidateAsync(
            ContentTypeDefinition contentDefinition,
            JsonNode inputNode,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ContentItemPayloadValidationResult(true, [], [], string.Empty));

        public ValueTask<string> GetGuidanceAsync(
            ContentTypeDefinition contentDefinition,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(string.Empty);
    }
}
