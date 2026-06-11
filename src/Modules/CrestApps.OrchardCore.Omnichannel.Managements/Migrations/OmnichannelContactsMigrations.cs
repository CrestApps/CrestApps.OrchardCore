using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
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
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.OmnichannelContact, part => part
            .Attachable()
            .WithDisplayName("Omnichannel Contact")
            .WithDescription("Provides a way to configure a content type to act as an omnichannel contact record.")
        );

        await EnsureDefaultContactIndexTableAsync();
        ShellScope.AddDeferredTask(ReindexPublishedContactsAsync);

        return 5;
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

    private async Task EnsureDefaultContactIndexTableAsync()
    {
        await RemoveLegacyCollectionContactIndexTableAsync();

        try
        {
            await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelContactIndex>(table => table
                .Column<string>("ContentItemId", column => column.WithLength(26))
                .Column<string>("PrimaryCellPhoneNumber", column => column.WithLength(50))
                .Column<string>("NormalizedPrimaryCellPhoneNumber", column => column.WithLength(50))
                .Column<string>("PrimaryHomePhoneNumber", column => column.WithLength(50))
                .Column<string>("NormalizedPrimaryHomePhoneNumber", column => column.WithLength(50))
                .Column<string>("PrimaryEmailAddress", column => column.WithLength(255))
                .Column<string>("TimeZoneId", column => column.WithLength(64))
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The default-collection OmnichannelContactIndex table may already exist.");
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
