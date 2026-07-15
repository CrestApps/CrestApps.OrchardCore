using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Services;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Defines database migrations for the Migrations module.
/// </summary>
public sealed class OmnichannelContactsMigrations : DataMigration
{
    private const string LegacyPhoneIndexTableName = "OmnichannelContactPhoneIndex";
    private const int ReindexBatchSize = 100;

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IStore _store;
    private readonly IDbConnectionAccessor _dbConnectionAccessor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactsMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelContactsMigrations(
        IContentDefinitionManager contentDefinitionManager,
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger<OmnichannelContactsMigrations> logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _store = store;
        _dbConnectionAccessor = dbConnectionAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Creates the omnichannel contact definition and final version-aware contact index.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.OmnichannelContact, part => part
            .Attachable()
            .WithDisplayName("Omnichannel Contact")
            .WithDescription("Provides a way to configure a content type to act as an omnichannel contact record.")
        );

        await CreateContactIndexTableAsync();
        await CreateContactIndexIndexesAsync();
        ScheduleContactDefinitionRepair();

        return 10;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
        {
            table.AddColumn<string>("NormalizedPrimaryCellPhoneNumber", column => column.WithLength(50));
            table.AddColumn<string>("NormalizedPrimaryHomePhoneNumber", column => column.WithLength(50));
        });

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OmnichannelContactIndex_NormalizedPrimaryCellPhoneNumber",
                "DocumentId",
                "NormalizedPrimaryCellPhoneNumber")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OmnichannelContactIndex_NormalizedPrimaryHomePhoneNumber",
                "DocumentId",
                "NormalizedPrimaryHomePhoneNumber")
        );

        return 2;
    }

    /// <summary>
    /// Updates the from2 async.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<string>("TimeZoneId", column => column.WithLength(64))
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'TimeZoneId' column may already exist on the OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex(
                    "IDX_OmnichannelContactIndex_TimeZoneId",
                    "DocumentId",
                    "TimeZoneId")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OmnichannelContactIndex_TimeZoneId' index may already exist.");
        }

        return 3;
    }

    /// <summary>
    /// Updates the from3 async.
    /// </summary>
    public async Task<int> UpdateFrom3Async()
    {
        await EnsureDefaultContactIndexTableAsync();

        return 4;
    }

    /// <summary>
    /// Updates the from4 async.
    /// </summary>
    public async Task<int> UpdateFrom4Async()
    {
        await EnsureDefaultContactIndexTableAsync();
        ShellScope.AddDeferredTask(ReindexPublishedContactsAsync);

        return 5;
    }

    /// <summary>
    /// Re-runs the default contact index repair for upgraded tenants to recover from earlier incomplete schema updates.
    /// </summary>
    public async Task<int> UpdateFrom5Async()
    {
        await EnsureDefaultContactIndexTableAsync();
        ShellScope.AddDeferredTask(ReindexPublishedContactsAsync);

        return 6;
    }

    /// <summary>
    /// Upgrades tenants at version 6 directly to the final version-aware contact index.
    /// </summary>
    public async Task<int> UpdateFrom6Async()
    {
        await EnsureDefaultContactIndexTableAsync();
        ShellScope.AddDeferredTask(ReindexContactVersionsAsync);

        return 9;
    }

    /// <summary>
    /// Merges the version-aware phone search fields into the shared contact index for tenants already at version 7.
    /// </summary>
    public async Task<int> UpdateFrom7Async()
    {
        await EnsureDefaultContactIndexTableAsync();
        await DropLegacyPhoneIndexTableAsync();
        ShellScope.AddDeferredTask(ReindexContactVersionsAsync);

        return 9;
    }

    /// <summary>
    /// Reuses the primary phone columns for national digits and removes the redundant national-number columns.
    /// </summary>
    public async Task<int> UpdateFrom8Async()
    {
        await EnsureDefaultContactIndexTableAsync();
        await RemoveRedundantNationalPhoneColumnsAsync();
        ShellScope.AddDeferredTask(ReindexContactVersionsAsync);

        return 9;
    }

    /// <summary>
    /// Repairs legacy omnichannel contact definitions without performing database work during tenant activation.
    /// </summary>
    public async Task<int> UpdateFrom9Async()
    {
        ScheduleContactDefinitionRepair();

        return 10;
    }

    private void ScheduleContactDefinitionRepair()
    {
        _logger.LogDebug("Scheduling deferred omnichannel contact definition repair.");

        ShellScope.AddDeferredTask(scope =>
            scope.ServiceProvider
                .GetRequiredService<OmnichannelContactDefinitionService>()
                .RepairOmnichannelContactContentTypesAsync());
    }

    private async Task CreateContactIndexTableAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelContactIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<bool>("Published", column => column.NotNull().WithDefault(false))
            .Column<bool>("Latest", column => column.NotNull().WithDefault(false))
            .Column<string>("PrimaryCellPhoneNumber", column => column.WithLength(50))
            .Column<string>("NormalizedPrimaryCellPhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryHomePhoneNumber", column => column.WithLength(50))
            .Column<string>("NormalizedPrimaryHomePhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryEmailAddress", column => column.WithLength(255))
            .Column<string>("TimeZoneId", column => column.WithLength(64))
        );
    }

    private async Task CreateContactIndexIndexesAsync()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OmnichannelContactIndex_DocumentId",
                "DocumentId",
                "ContentItemId")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OmnichannelContactIndex_NormalizedPrimaryCellPhoneNumber",
                "DocumentId",
                "NormalizedPrimaryCellPhoneNumber")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OmnichannelContactIndex_NormalizedPrimaryHomePhoneNumber",
                "DocumentId",
                "NormalizedPrimaryHomePhoneNumber")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OmnichannelContactIndex_TimeZoneId",
                "DocumentId",
                "TimeZoneId")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OCIndex_ContentItemLatest",
                "ContentItemId",
                "Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OCIndex_ContentItemPublished",
                "ContentItemId",
                "Published")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OCIndex_E164Cell",
                "NormalizedPrimaryCellPhoneNumber",
                "Published",
                "Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OCIndex_PrimaryCell",
                "PrimaryCellPhoneNumber",
                "Published",
                "Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OCIndex_E164Home",
                "NormalizedPrimaryHomePhoneNumber",
                "Published",
                "Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OCIndex_PrimaryHome",
                "PrimaryHomePhoneNumber",
                "Published",
                "Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OCIndex_TimeZoneVersion",
                "TimeZoneId",
                "Published",
                "Latest")
        );
    }

    private async Task EnsureDefaultContactIndexTableAsync()
    {
        await RemoveLegacyCollectionContactIndexTableAsync();

        try
        {
            await CreateContactIndexTableAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The default-collection OmnichannelContactIndex table may already exist.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<bool>("Published", column => column.NotNull().WithDefault(false))
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'Published' column may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<bool>("Latest", column => column.NotNull().WithDefault(false))
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'Latest' column may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<string>("NormalizedPrimaryCellPhoneNumber", column => column.WithLength(50))
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'NormalizedPrimaryCellPhoneNumber' column may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<string>("NormalizedPrimaryHomePhoneNumber", column => column.WithLength(50))
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'NormalizedPrimaryHomePhoneNumber' column may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<string>("TimeZoneId", column => column.WithLength(64))
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'TimeZoneId' column may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex("IDX_OmnichannelContactIndex_DocumentId",
                    "DocumentId",
                    "ContentItemId"
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OmnichannelContactIndex_DocumentId' index may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex(
                    "IDX_OmnichannelContactIndex_NormalizedPrimaryCellPhoneNumber",
                    "DocumentId",
                    "NormalizedPrimaryCellPhoneNumber")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OmnichannelContactIndex_NormalizedPrimaryCellPhoneNumber' index may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex(
                    "IDX_OmnichannelContactIndex_NormalizedPrimaryHomePhoneNumber",
                    "DocumentId",
                    "NormalizedPrimaryHomePhoneNumber")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OmnichannelContactIndex_NormalizedPrimaryHomePhoneNumber' index may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex(
                    "IDX_OmnichannelContactIndex_TimeZoneId",
                    "DocumentId",
                    "TimeZoneId")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OmnichannelContactIndex_TimeZoneId' index may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex(
                    "IDX_OCIndex_ContentItemLatest",
                    "ContentItemId",
                    "Latest")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OCIndex_ContentItemLatest' index may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex(
                    "IDX_OCIndex_ContentItemPublished",
                    "ContentItemId",
                    "Published")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OCIndex_ContentItemPublished' index may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex(
                    "IDX_OCIndex_E164Cell",
                    "NormalizedPrimaryCellPhoneNumber",
                    "Published",
                    "Latest")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OCIndex_E164Cell' index may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex(
                    "IDX_OCIndex_PrimaryCell",
                    "PrimaryCellPhoneNumber",
                    "Published",
                    "Latest")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OCIndex_PrimaryCell' index may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex(
                    "IDX_OCIndex_E164Home",
                    "NormalizedPrimaryHomePhoneNumber",
                    "Published",
                    "Latest")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OCIndex_E164Home' index may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex(
                    "IDX_OCIndex_PrimaryHome",
                    "PrimaryHomePhoneNumber",
                    "Published",
                    "Latest")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OCIndex_PrimaryHome' index may already exist on the default-collection OmnichannelContactIndex table.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
                .CreateIndex(
                    "IDX_OCIndex_TimeZoneVersion",
                    "TimeZoneId",
                    "Published",
                    "Latest")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OCIndex_TimeZoneVersion' index may already exist on the default-collection OmnichannelContactIndex table.");
        }
    }

    private async Task RemoveRedundantNationalPhoneColumnsAsync()
    {
        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.DropIndex("IDX_OCIndex_NationalCell")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The obsolete 'IDX_OCIndex_NationalCell' index may already be removed.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.DropIndex("IDX_OCIndex_NationalHome")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The obsolete 'IDX_OCIndex_NationalHome' index may already be removed.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.DropColumn("NationalPrimaryCellPhoneNumber")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The obsolete 'NationalPrimaryCellPhoneNumber' column may already be removed.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.DropColumn("NationalPrimaryHomePhoneNumber")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The obsolete 'NationalPrimaryHomePhoneNumber' column may already be removed.");
        }
    }

    private async Task DropLegacyPhoneIndexTableAsync()
    {
        var dialect = _store.Configuration.SqlDialect;
        var table = $"{_store.Configuration.TablePrefix}{LegacyPhoneIndexTableName}";
        var quotedTable = dialect.QuoteForTableName(table, _store.Configuration.Schema);

        try
        {
            await using var connection = _dbConnectionAccessor.CreateConnection();
            await connection.OpenAsync();
            await connection.ExecuteAsync($"drop table {quotedTable}");

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Dropped the obsolete default-collection contact phone index table '{TableName}'.",
                    table);
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    ex,
                    "The obsolete default-collection contact phone index table '{TableName}' was not dropped because it was unavailable or already removed.",
                    table);
            }
        }
    }

    private async Task RemoveLegacyCollectionContactIndexTableAsync()
    {
        var dialect = _store.Configuration.SqlDialect;
        var tableName = _store.Configuration.TableNameConvention.GetIndexTable(typeof(OmnichannelContactIndex), OmnichannelConstants.CollectionName);
        var table = $"{_store.Configuration.TablePrefix}{tableName}";
        var quotedTable = dialect.QuoteForTableName(table, _store.Configuration.Schema);

        await using var connection = _dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        try
        {
            var rowCount = await connection.ExecuteScalarAsync<int>($"select count(*) from {quotedTable}");

            if (rowCount > 0)
            {
                _logger.LogWarning(
                    "Skipping removal of the legacy Omnichannel collection contact index table because it still contains {RowCount} row(s).",
                    rowCount);

                return;
            }

            await connection.ExecuteAsync($"drop table {quotedTable}");

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Dropped the legacy Omnichannel collection contact index table '{TableName}' so the default-collection contact index can be recreated.",
                    table);
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "The legacy Omnichannel collection contact index table '{TableName}' was not removed because it was not available for cleanup.", table);
            }
        }
    }

    private static async Task ReindexPublishedContactsAsync(ShellScope scope)
    {
        var contentDefinitionManager = scope.ServiceProvider.GetRequiredService<IContentDefinitionManager>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OmnichannelContactsMigrations>>();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        var contentTypes = await GetContentTypesWithOmnichannelContactPartAsync(contentDefinitionManager);

        if (contentTypes.Length == 0)
        {
            return;
        }

        var documentId = 0L;
        var reindexedCount = 0;

        while (true)
        {
            await using var session = store.CreateSession();

            var batch = await session.Query<ContentItem, ContentItemIndex>(index =>
                index.Published && index.ContentType.IsIn(contentTypes) && index.DocumentId > documentId)
                .OrderBy(index => index.DocumentId)
                .Take(ReindexBatchSize)
                .ListAsync();

            if (!batch.Any())
            {
                break;
            }

            foreach (var contentItem in batch)
            {
                documentId = Math.Max(documentId, contentItem.Id);
                await session.SaveAsync(contentItem);
                reindexedCount++;
            }

            await session.SaveChangesAsync();
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Reindexed {ReindexedCount} published omnichannel contact content item(s) after repairing the default contact index table.",
                reindexedCount);
        }
    }

    private static async Task ReindexContactVersionsAsync(ShellScope scope)
    {
        var contentDefinitionManager = scope.ServiceProvider.GetRequiredService<IContentDefinitionManager>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OmnichannelContactsMigrations>>();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        var contentTypes = await GetContentTypesWithOmnichannelContactPartAsync(contentDefinitionManager);

        if (contentTypes.Length == 0)
        {
            return;
        }

        var documentId = 0L;
        var reindexedCount = 0;

        while (true)
        {
            await using var session = store.CreateSession();

            var batch = await session.Query<ContentItem, ContentItemIndex>(index =>
                (index.Published || index.Latest) &&
                index.ContentType.IsIn(contentTypes) &&
                index.DocumentId > documentId)
                .OrderBy(index => index.DocumentId)
                .Take(ReindexBatchSize)
                .ListAsync();

            if (!batch.Any())
            {
                break;
            }

            foreach (var contentItem in batch)
            {
                documentId = Math.Max(documentId, contentItem.Id);
                await session.SaveAsync(contentItem);
                reindexedCount++;
            }

            await session.SaveChangesAsync();
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Reindexed {ReindexedCount} published or latest omnichannel contact content item version(s) after upgrading the shared contact index.",
                reindexedCount);
        }
    }

    private static async Task<string[]> GetContentTypesWithOmnichannelContactPartAsync(IContentDefinitionManager contentDefinitionManager)
    {
        var typeDefinitions = await contentDefinitionManager.ListTypeDefinitionsAsync();

        return typeDefinitions
            .Where(type => type.Parts.Any(part =>
                string.Equals(part.PartDefinition.Name, OmnichannelConstants.ContentParts.OmnichannelContact, StringComparison.Ordinal)))
            .Select(type => type.Name)
            .ToArray();
    }
}
