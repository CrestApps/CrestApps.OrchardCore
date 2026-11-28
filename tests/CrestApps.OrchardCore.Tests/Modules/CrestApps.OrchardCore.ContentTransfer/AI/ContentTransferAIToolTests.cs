using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.AI;
using CrestApps.OrchardCore.ContentTransfer.FileFormats;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.ContentTransfer.Migrations;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.DataConnectors;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Filters.Query;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer.AI;

/// <summary>
/// The content transfer tools check the caller's permission per content type, as the Import and Export pages do, show
/// an export only to the user who made it, pause and resume imports only from the states the Import list allows, and
/// never mark an entry as deleting without queueing its deletion. The entries live in a real SQLite database.
/// </summary>
public sealed class ContentTransferAIToolTests : IAsyncLifetime
{
    private const string OtherUserId = "user-2";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-transfer-{Guid.NewGuid():N}.db");
    private readonly Mock<IContentDefinitionManager> _definitions = new();
    private readonly Mock<IContentImportManager> _importManager = new();
    private IStore _store;
    private YesSqlSession _session;

    public static TheoryData<string> ToolNames => new()
    {
        ListContentTransferTypesTool.TheName,
        ListContentTransferEntriesTool.TheName,
        GetContentTransferEntryTool.TheName,
        SetContentImportStateTool.TheName,
        DeleteContentTransferEntryTool.TheName,
    };

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False").SetTablePrefix("tenant1_"));
        _store.RegisterIndexes([new ContentTransferEntryIndexProvider()]);
        await _store.InitializeAsync(cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var migration = new ContentTransferMigrations(NullLogger<ContentTransferMigrations>.Instance)
            {
                SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction),
            };

            await migration.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        _session = _store.CreateSession();

        _definitions.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync(
        [
            new ContentTypeDefinition("Product", "Product"),
            new ContentTypeDefinition("Article", "Article"),
            new ContentTypeDefinition("Secret", "Secret", [], new JsonObject
            {
                [nameof(ContentTypeTransferSettings)] = new JsonObject
                {
                    [nameof(ContentTypeTransferSettings.AllowBulkImport)] = false,
                    [nameof(ContentTypeTransferSettings.AllowBulkExport)] = false,
                },
            }),
        ]);

        _importManager.Setup(manager => manager.GetColumnsAsync(It.IsAny<ImportContentContext>())).ReturnsAsync(
        [
            new ImportColumn { Name = "Title", IsRequired = true },
            new ImportColumn { Name = "Sku", Type = ImportColumnType.ImportOnly },
            new ImportColumn { Name = "ContentItemId", Type = ImportColumnType.ExportOnly },
        ]);
    }

    public async ValueTask DisposeAsync()
    {
        await _session.DisposeAsync();
        _store.Dispose();

        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
        }
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        await AddEntryAsync("import1", ContentTransferDirection.Import, ContentTransferEntryStatus.Processing);
        var services = Build(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new() { ["entryId"] = "import1", ["state"] = "Paused" });

        // Assert
        Assert.Equal(ContentTransferAIToolBase.NotAllowedMessage, result);
        Assert.Equal(ContentTransferEntryStatus.Processing, (await FindAsync("import1")).Status);
    }

    public static TheoryData<string, string> ToolsAndPermissions => new()
    {
        { ListContentTransferTypesTool.TheName, ContentTransferPermissions.ImportContentFromFile.Name + "," + ContentTransferPermissions.ExportContentFromFile.Name },
        { ListContentTransferEntriesTool.TheName, ContentTransferPermissions.ListContentTransferEntries.Name },
        { GetContentTransferEntryTool.TheName, ContentTransferPermissions.ImportContentFromFile.Name },
        { SetContentImportStateTool.TheName, ContentTransferPermissions.ImportContentFromFile.Name },
        { DeleteContentTransferEntryTool.TheName, ContentTransferPermissions.DeleteContentTransferEntries.Name },
    };

    [Theory]
    [MemberData(nameof(ToolsAndPermissions))]
    public async Task Tool_RefusesACallerWithoutItsPermission(string toolName, string permissions)
    {
        // Arrange
        var denied = permissions.Split(',');
        await AddEntryAsync("import1", ContentTransferDirection.Import, ContentTransferEntryStatus.Processing);
        var services = Build(authorize: (permission, _) => !denied.Contains(permission.Name));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new() { ["entryId"] = "import1", ["state"] = "Paused" });

        // Assert
        Assert.Equal(ContentTransferAIToolBase.NotAllowedMessage, result);
        Assert.Equal(ContentTransferEntryStatus.Processing, (await FindAsync("import1")).Status);
        Assert.All(denied, name => Assert.Contains(services.GetRequiredService<FakeAuthorizationService>().Checks, check => check.Permission == name));
    }

    [Fact]
    public async Task DeleteContentTransferEntry_QueuesAgain_AnEntryAlreadyMarkedAsDeleting()
    {
        // Arrange
        await AddEntryAsync("import1", ContentTransferDirection.Import, ContentTransferEntryStatus.Deleting);
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteContentTransferEntryTool(), services, new() { ["entryId"] = "import1" });

        // Assert: the test has no shell scope, so the tool reaches the queueing step and says it could not queue there.
        Assert.Contains("could not be queued", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListContentTransferTypes_ListsTheTypesTheUserMayTransfer_WithTheirColumns()
    {
        // Arrange
        var services = Build(authorize: (permission, resource) =>
            !(permission.Name == ContentTransferPermissions.ImportContentFromFile.Name && Equals(resource, "Article")));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListContentTransferTypesTool(), services, new() { ["contentType"] = "Product" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var types = json.GetProperty("contentTypes").EnumerateArray().ToDictionary(type => type.GetProperty("name").GetString());
        Assert.Equal(new[] { "Article", "Product" }, types.Keys.Order());
        Assert.False(types["Article"].GetProperty("canImport").GetBoolean());
        Assert.True(types["Article"].GetProperty("canExport").GetBoolean());
        Assert.Equal(".csv", json.GetProperty("formats")[0].GetString());
        var columns = json.GetProperty("columns");
        Assert.Equal(new[] { "Title", "Sku" }, columns.GetProperty("import").EnumerateArray().Select(column => column.GetProperty("name").GetString()));
        Assert.Equal(new[] { "Title", "ContentItemId" }, columns.GetProperty("export").EnumerateArray().Select(column => column.GetProperty("name").GetString()));
        var checks = services.GetRequiredService<FakeAuthorizationService>().Checks;
        Assert.Contains(checks, check => check.Permission == ContentTransferPermissions.ImportContentFromFile.Name && Equals(check.Resource, "Product"));
    }

    [Fact]
    public async Task ListContentTransferTypes_RefusesATypeThatDoesNotAllowBulkTransfer()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListContentTransferTypesTool(), services, new() { ["contentType"] = "Secret" });

        // Assert
        Assert.Contains("can't bulk import or export the content type 'Secret'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListContentTransferEntries_ListsImportsWithAStatusFilter_AndOnlyTheUsersOwnExports()
    {
        // Arrange
        await AddEntryAsync("import1", ContentTransferDirection.Import, ContentTransferEntryStatus.Failed);
        await AddEntryAsync("import2", ContentTransferDirection.Import, ContentTransferEntryStatus.Completed);
        await AddEntryAsync("export1", ContentTransferDirection.Export, ContentTransferEntryStatus.Completed);
        await AddEntryAsync("export2", ContentTransferDirection.Export, ContentTransferEntryStatus.Completed, owner: OtherUserId);
        var services = Build();

        // Act
        var imports = await AgentToolTestContext.InvokeAsync(new ListContentTransferEntriesTool(), services, new() { ["status"] = "Failed" });
        var exports = await AgentToolTestContext.InvokeAsync(new ListContentTransferEntriesTool(), services, new() { ["direction"] = "Export" });

        // Assert
        var importItems = AgentToolTestContext.ReadJson(imports).GetProperty("items");
        Assert.Equal("import1", Assert.Single(importItems.EnumerateArray()).GetProperty("entryId").GetString());
        var exportItems = AgentToolTestContext.ReadJson(exports).GetProperty("items");
        Assert.Equal("export1", Assert.Single(exportItems.EnumerateArray()).GetProperty("entryId").GetString());
    }

    [Fact]
    public async Task GetContentTransferEntry_ReturnsTheProgressAndFailedRows_CheckedOnTheEntrysContentType()
    {
        // Arrange
        var entry = new ContentTransferEntry { EntryId = "import1", ContentType = "Product", Direction = ContentTransferDirection.Import, Status = ContentTransferEntryStatus.CompletedWithErrors, Owner = AgentToolTestContext.UserId, CreatedUtc = DateTime.UtcNow };
        entry.Put(new ImportFileProcessStatsPart
        {
            TotalRecords = 10,
            TotalProcessed = 10,
            ImportedCount = 8,
            Errors = [3, 7],
            ErrorMessages = new Dictionary<int, string> { [3] = "The SKU is missing." },
            ErrorCategories = new Dictionary<string, HashSet<int>> { ["Invalid value"] = [7] },
        });
        await SaveAsync(entry);
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentTransferEntryTool(), services, new() { ["entryId"] = "import1" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal(8, json.GetProperty("progress").GetProperty("importedRows").GetInt32());
        Assert.Equal("The SKU is missing.", json.GetProperty("errors")[0].GetProperty("message").GetString());
        Assert.Equal("Invalid value", json.GetProperty("errors")[1].GetProperty("message").GetString());
        Assert.Contains(services.GetRequiredService<FakeAuthorizationService>().Checks, check => check.Permission == ContentTransferPermissions.ImportContentFromFile.Name && Equals(check.Resource, "Product"));
    }

    [Fact]
    public async Task GetContentTransferEntry_HidesAnotherUsersExport_AndSaysWhenTheEntryIsMissing()
    {
        // Arrange
        await AddEntryAsync("export2", ContentTransferDirection.Export, ContentTransferEntryStatus.Completed, owner: OtherUserId);
        var services = Build();

        // Act
        var other = await AgentToolTestContext.InvokeAsync(new GetContentTransferEntryTool(), services, new() { ["entryId"] = "export2" });
        var missing = await AgentToolTestContext.InvokeAsync(new GetContentTransferEntryTool(), services, new() { ["entryId"] = "nope" });

        // Assert
        Assert.Contains("No content transfer entry has the ID 'export2'", other, StringComparison.Ordinal);
        Assert.Contains("No content transfer entry has the ID 'nope'", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetContentImportState_PausesAProcessingImport_AndResumesAPausedOne()
    {
        // Arrange
        await AddEntryAsync("import1", ContentTransferDirection.Import, ContentTransferEntryStatus.Processing);
        var services = Build();

        // Act
        var paused = await AgentToolTestContext.InvokeAsync(new SetContentImportStateTool(), services, new() { ["entryId"] = "import1", ["state"] = "Paused" });
        var resumed = await AgentToolTestContext.InvokeAsync(new SetContentImportStateTool(), services, new() { ["entryId"] = "import1", ["state"] = "Resumed" });

        // Assert
        Assert.Equal("Paused", AgentToolTestContext.ReadJson(paused).GetProperty("status").GetString());
        Assert.Equal("Processing", AgentToolTestContext.ReadJson(resumed).GetProperty("status").GetString());
        Assert.Equal(ContentTransferEntryStatus.Processing, (await FindAsync("import1")).Status);
    }

    [Theory]
    [InlineData(ContentTransferEntryStatus.Completed, "Resumed")]
    [InlineData(ContentTransferEntryStatus.Processing, "Resumed")]
    [InlineData(ContentTransferEntryStatus.Paused, "Paused")]
    public async Task SetContentImportState_RefusesAChangeTheImportListDoesNotAllow(ContentTransferEntryStatus status, string state)
    {
        // Arrange
        await AddEntryAsync("import1", ContentTransferDirection.Import, status);
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetContentImportStateTool(), services, new() { ["entryId"] = "import1", ["state"] = state });

        // Assert
        Assert.StartsWith("Nothing was changed:", result, StringComparison.Ordinal);
        Assert.Equal(status, (await FindAsync("import1")).Status);
    }

    [Fact]
    public async Task DeleteContentTransferEntry_RefusesAnotherUsersExport()
    {
        // Arrange
        await AddEntryAsync("export2", ContentTransferDirection.Export, ContentTransferEntryStatus.Completed, owner: OtherUserId);
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteContentTransferEntryTool(), services, new() { ["entryId"] = "export2" });

        // Assert
        Assert.StartsWith("Nothing was deleted:", result, StringComparison.Ordinal);
        Assert.Equal(ContentTransferEntryStatus.Completed, (await FindAsync("export2")).Status);
    }

    [Fact]
    public async Task DeleteContentTransferEntry_DoesNotMarkTheEntry_WhenItsDeletionCannotBeQueued()
    {
        // Arrange
        await AddEntryAsync("import1", ContentTransferDirection.Import, ContentTransferEntryStatus.Completed);
        var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteContentTransferEntryTool(), services, new() { ["entryId"] = "import1" });

        // Assert
        Assert.Contains("could not be queued", result, StringComparison.Ordinal);
        Assert.Equal(ContentTransferEntryStatus.Completed, (await FindAsync("import1")).Status);
        Assert.Contains(services.GetRequiredService<FakeAuthorizationService>().Checks, check => check.Permission == ContentTransferPermissions.DeleteContentTransferEntries.Name);
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.NewAsync(It.IsAny<string>())).ReturnsAsync((string type) => new ContentItem { ContentType = type });

        var builder = new QueryEngineBuilder<ContentTransferEntry>();
        new DefaultContentTransferEntryAdminListFilterProvider().Build(builder);

        services.AddSingleton(_session);
        services.AddSingleton(_definitions.Object);
        services.AddSingleton(_importManager.Object);
        services.AddSingleton(contentManager.Object);
        services.AddSingleton<IContentTransferFileFormatProvider>(new CsvContentTransferFileFormatProvider());
        services.AddSingleton<IContentTransferEntryAdminListFilterParser>(new DefaultContentTypeEntryAdminListFilterParser(builder.Build()));
        services.AddSingleton<IContentTransferEntryAdminListQueryService>(provider => new DefaultContentTransferEntryAdminListQueryService(_session, provider));
        services.AddSingleton<IDistributedLock>(new InMemoryDistributedLock());
        services.AddSingleton(new Mock<IContentTransferFileStore>().Object);
        services.AddSingleton<IClock>(new FixedClock());
        services.AddSingleton<IContentTransferEntryManager, ContentTransferEntryManager>();

        return services.BuildServiceProvider();
    }

    private Task AddEntryAsync(string entryId, ContentTransferDirection direction, ContentTransferEntryStatus status, string owner = AgentToolTestContext.UserId)
        => SaveAsync(new ContentTransferEntry
        {
            EntryId = entryId,
            ContentType = "Product",
            Direction = direction,
            Status = status,
            Owner = owner,
            UploadedFileName = entryId + ".csv",
            CreatedUtc = DateTime.UtcNow,
        });

    private async Task SaveAsync(ContentTransferEntry entry)
    {
        await _session.SaveAsync(entry, false, collection: null, TestContext.Current.CancellationToken);
        await _session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ContentTransferEntry> FindAsync(string entryId)
    {
        await using var session = _store.CreateSession();

        return await session.Query<ContentTransferEntry, ContentTransferEntryIndex>(index => index.EntryId == entryId).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListContentTransferTypesTool.TheName => new ListContentTransferTypesTool(),
            ListContentTransferEntriesTool.TheName => new ListContentTransferEntriesTool(),
            GetContentTransferEntryTool.TheName => new GetContentTransferEntryTool(),
            SetContentImportStateTool.TheName => new SetContentImportStateTool(),
            DeleteContentTransferEntryTool.TheName => new DeleteContentTransferEntryTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName)),
        };
}
