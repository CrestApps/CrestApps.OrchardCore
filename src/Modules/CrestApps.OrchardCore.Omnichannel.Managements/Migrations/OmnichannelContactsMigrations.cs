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
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Services;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Defines database migrations for the Migrations module.
/// </summary>
public sealed class OmnichannelContactsMigrations : OmnichannelIndexMigration
{
    private const string LegacyPhoneIndexTableName = "OmnichannelContactPhoneIndex";
    private const int ReindexBatchSize = 100;
    private const string EmailIndexName = "IDX_OCIndex_Email";

    private static readonly string[] _emailIndexColumns = ["NormalizedPrimaryEmailAddress", "Published", "Latest"];

    private static readonly (string Name, string[] Columns)[] _contactIndexIndexes =
    [
        ("IDX_OmnichannelContactIndex_DocumentId", ["DocumentId", "ContentItemId"]),
        ("IDX_OmnichannelContactIndex_NormalizedPrimaryCellPhoneNumber", ["DocumentId", "NormalizedPrimaryCellPhoneNumber"]),
        ("IDX_OmnichannelContactIndex_NormalizedPrimaryHomePhoneNumber", ["DocumentId", "NormalizedPrimaryHomePhoneNumber"]),
        ("IDX_OmnichannelContactIndex_TimeZoneId", ["DocumentId", "TimeZoneId"]),
        ("IDX_OCIndex_ContentItemLatest", ["ContentItemId", "Latest"]),
        ("IDX_OCIndex_ContentItemPublished", ["ContentItemId", "Published"]),
        ("IDX_OCIndex_E164Cell", ["NormalizedPrimaryCellPhoneNumber", "Published", "Latest"]),
        ("IDX_OCIndex_PrimaryCell", ["PrimaryCellPhoneNumber", "Published", "Latest"]),
        ("IDX_OCIndex_E164Home", ["NormalizedPrimaryHomePhoneNumber", "Published", "Latest"]),
        ("IDX_OCIndex_PrimaryHome", ["PrimaryHomePhoneNumber", "Published", "Latest"]),
        ("IDX_OCIndex_TimeZoneVersion", ["TimeZoneId", "Published", "Latest"]),
        ("IDX_OCIndex_ContentType", ["ContentType", "Published", "Latest"]),
    ];

    private readonly IContentDefinitionManager _contentDefinitionManager;

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
        : base(store, dbConnectionAccessor, logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
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

        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.OmnichannelSubject, part => part
            .Attachable()
            .WithDisplayName("Omnichannel Subject")
            .WithDescription("Provides a way to configure a content type to act as an omnichannel subject record.")
        );

        await CreateContactIndexTableAsync(SchemaBuilder);
        await CreateContactIndexIndexesAsync(SchemaBuilder);
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
            table.CreateIndex(EmailIndexName, _emailIndexColumns));
        ScheduleContactDefinitionRepair();

        return 13;
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
        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<string>("TimeZoneId", column => column.WithLength(64))),
            "The 'TimeZoneId' column may already exist on the OmnichannelContactIndex table.");

        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.CreateIndex("IDX_OmnichannelContactIndex_TimeZoneId", "DocumentId", "TimeZoneId")),
            "The 'IDX_OmnichannelContactIndex_TimeZoneId' index may already exist.");

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

        return 9;
    }

    /// <summary>
    /// Merges the version-aware phone search fields into the shared contact index for tenants already at version 7.
    /// </summary>
    public async Task<int> UpdateFrom7Async()
    {
        await EnsureDefaultContactIndexTableAsync();
        await DropLegacyPhoneIndexTableAsync();

        return 9;
    }

    /// <summary>
    /// Reuses the primary phone columns for national digits and removes the redundant national-number columns.
    /// </summary>
    public async Task<int> UpdateFrom8Async()
    {
        await EnsureDefaultContactIndexTableAsync();
        await RemoveRedundantNationalPhoneColumnsAsync();

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

    public async Task<int> UpdateFrom10Async()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.OmnichannelSubject, part => part
            .Attachable()
            .WithDisplayName("Omnichannel Subject")
            .WithDescription("Provides a way to configure a content type to act as an omnichannel subject record.")
        );

        return 11;
    }

    /// <summary>
    /// Adds the content type and converted-lead columns, so a lookup can tell a lead from a contact without loading
    /// the item, and fills the content type of every existing row from the content item index. No content item is
    /// saved again: the value is copied in SQL, one statement per content type.
    /// </summary>
    public async Task<int> UpdateFrom11Async()
    {
        await EnsureColumnExistsAsync<OmnichannelContactIndex>(
            collection: null,
            columnName: nameof(OmnichannelContactIndex.ContentType),
            addColumn: table => table.AddColumn<string>(nameof(OmnichannelContactIndex.ContentType), column => column.WithLength(255)),
            operation: "add the 'ContentType' column to the contact index");

        await EnsureColumnExistsAsync<OmnichannelContactIndex>(
            collection: null,
            columnName: nameof(OmnichannelContactIndex.IsConverted),
            addColumn: table => table.AddColumn<bool>(nameof(OmnichannelContactIndex.IsConverted), column => column.NotNull().WithDefault(false)),
            operation: "add the 'IsConverted' column to the contact index");

        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.CreateIndex("IDX_OCIndex_ContentType", "ContentType", "Published", "Latest")),
            "create the 'IDX_OCIndex_ContentType' index");

        var failure = await TryApplyIsolatedAsync(BackfillContactIndexContentTypesAsync);

        if (failure is not null)
        {
            // A row left without a content type is ranked as a contact, which is what every row was before the
            // column existed, and the next save of the contact fills it. The failure is still worth seeing.
            Logger.LogWarning(failure, "The content type of the existing contact index rows could not be filled in.");
        }

        return 12;
    }

    /// <summary>
    /// Adds the canonical (lower-case) primary email column, so an inbound email finds its contact however the sender
    /// typed their address, and fills it from the email already indexed. No content item is saved again: the value is
    /// copied in SQL, and an address stored with a display name is corrected the next time its contact is saved.
    /// </summary>
    public async Task<int> UpdateFrom12Async()
    {
        await EnsureColumnExistsAsync<OmnichannelContactIndex>(
            collection: null,
            columnName: nameof(OmnichannelContactIndex.NormalizedPrimaryEmailAddress),
            addColumn: table => table.AddColumn<string>(nameof(OmnichannelContactIndex.NormalizedPrimaryEmailAddress), column => column.WithLength(255)),
            operation: "add the 'NormalizedPrimaryEmailAddress' column to the contact index");

        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.CreateIndex(EmailIndexName, _emailIndexColumns)),
            $"create the '{EmailIndexName}' index");

        var failure = await TryApplyIsolatedAsync(BackfillNormalizedEmailAddressesAsync);

        if (failure is not null)
        {
            // A row left without the canonical email is filled the next time its contact is saved; until then an
            // inbound email from that contact starts as an unknown sender.
            Logger.LogWarning(failure, "The canonical primary email of the existing contact index rows could not be filled in.");
        }

        return 13;
    }

    private async Task BackfillNormalizedEmailAddressesAsync(ISchemaBuilder builder)
    {
        var dialect = Store.Configuration.SqlDialect;
        var contactTable = dialect.QuoteForTableName(
            $"{Store.Configuration.TablePrefix}{Store.Configuration.TableNameConvention.GetIndexTable(typeof(OmnichannelContactIndex))}",
            Store.Configuration.Schema);
        var primary = dialect.QuoteForColumnName(nameof(OmnichannelContactIndex.PrimaryEmailAddress));
        var normalized = dialect.QuoteForColumnName(nameof(OmnichannelContactIndex.NormalizedPrimaryEmailAddress));

        // LOWER, LTRIM and RTRIM are spelled the same by every supported database.
        var updated = await builder.Connection.ExecuteAsync(
            $"UPDATE {contactTable} SET {normalized} = LOWER(LTRIM(RTRIM({primary}))) WHERE {primary} IS NOT NULL AND {normalized} IS NULL",
            transaction: builder.Transaction);

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation("Filled in the canonical primary email of {RowCount} contact index row(s).", updated);
        }
    }

    private async Task BackfillContactIndexContentTypesAsync(ISchemaBuilder builder)
    {
        var dialect = Store.Configuration.SqlDialect;
        var schema = Store.Configuration.Schema;
        var prefix = Store.Configuration.TablePrefix;
        var contactTable = dialect.QuoteForTableName(
            $"{prefix}{Store.Configuration.TableNameConvention.GetIndexTable(typeof(OmnichannelContactIndex))}",
            schema);
        var contentItemTable = dialect.QuoteForTableName(
            $"{prefix}{Store.Configuration.TableNameConvention.GetIndexTable(typeof(ContentItemIndex))}",
            schema);
        var documentId = dialect.QuoteForColumnName("DocumentId");
        var contentType = dialect.QuoteForColumnName("ContentType");

        // One statement per content type keeps the update free of a correlated reference to the updated table,
        // which the supported databases spell differently.
        var contentTypes = (await builder.Connection.QueryAsync<string>(
            $"SELECT DISTINCT {contentType} FROM {contentItemTable} WHERE {documentId} IN (SELECT {documentId} FROM {contactTable} WHERE {contentType} IS NULL)",
            transaction: builder.Transaction))
            .Where(value => !string.IsNullOrEmpty(value))
            .ToArray();

        var updated = 0;

        foreach (var value in contentTypes)
        {
            updated += await builder.Connection.ExecuteAsync(
                $"UPDATE {contactTable} SET {contentType} = @ContentType WHERE {contentType} IS NULL AND {documentId} IN (SELECT {documentId} FROM {contentItemTable} WHERE {contentType} = @ContentType)",
                new { ContentType = value },
                builder.Transaction);
        }

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation(
                "Filled in the content type of {RowCount} contact index row(s) across {ContentTypeCount} content type(s).",
                updated,
                contentTypes.Length);
        }
    }

    private void ScheduleContactDefinitionRepair()
    {
        Logger.LogDebug("Scheduling deferred omnichannel contact definition repair.");

        ShellScope.AddDeferredTask(scope =>
            scope.ServiceProvider
                .GetRequiredService<OmnichannelContactDefinitionService>()
                .RepairOmnichannelContactContentTypesAsync());
    }

    private static async Task CreateContactIndexTableAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelContactIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<bool>("IsConverted", column => column.NotNull().WithDefault(false))
            .Column<bool>("Published", column => column.NotNull().WithDefault(false))
            .Column<bool>("Latest", column => column.NotNull().WithDefault(false))
            .Column<string>("PrimaryCellPhoneNumber", column => column.WithLength(50))
            .Column<string>("NormalizedPrimaryCellPhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryHomePhoneNumber", column => column.WithLength(50))
            .Column<string>("NormalizedPrimaryHomePhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryEmailAddress", column => column.WithLength(255))
            .Column<string>("NormalizedPrimaryEmailAddress", column => column.WithLength(255))
            .Column<string>("TimeZoneId", column => column.WithLength(64))
        );
    }

    private static async Task CreateContactIndexIndexesAsync(ISchemaBuilder schemaBuilder)
    {
        foreach (var (name, columns) in _contactIndexIndexes)
        {
            await schemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.CreateIndex(name, columns));
        }
    }

    private async Task EnsureDefaultContactIndexTableAsync()
    {
        await RemoveLegacyCollectionContactIndexTableAsync();

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation(
                "Ensuring the default-collection OmnichannelContactIndex schema using the '{SqlDialect}' SQL dialect with table prefix '{TablePrefix}'.",
                Store.Configuration.SqlDialect.Name,
                Store.Configuration.TablePrefix);
        }

        await ApplyIsolatedSchemaChangeAsync(
            CreateContactIndexTableAsync,
            "create the table");

        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<bool>("Published", column => column.NotNull().WithDefault(false))),
            "add the 'Published' column");

        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<bool>("Latest", column => column.NotNull().WithDefault(false))),
            "add the 'Latest' column");

        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<string>("NormalizedPrimaryCellPhoneNumber", column => column.WithLength(50))),
            "add the 'NormalizedPrimaryCellPhoneNumber' column");

        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<string>("NormalizedPrimaryHomePhoneNumber", column => column.WithLength(50))),
            "add the 'NormalizedPrimaryHomePhoneNumber' column");

        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.AddColumn<string>("TimeZoneId", column => column.WithLength(64))),
            "add the 'TimeZoneId' column");

        foreach (var (name, columns) in _contactIndexIndexes)
        {
            await ApplyIsolatedSchemaChangeAsync(
                builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                    table.CreateIndex(name, columns)),
                $"create the '{name}' index");
        }
    }

    private async Task RemoveRedundantNationalPhoneColumnsAsync()
    {
        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.DropIndex("IDX_OCIndex_NationalCell")),
            "drop the obsolete 'IDX_OCIndex_NationalCell' index");

        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.DropIndex("IDX_OCIndex_NationalHome")),
            "drop the obsolete 'IDX_OCIndex_NationalHome' index");

        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.DropColumn("NationalPrimaryCellPhoneNumber")),
            "drop the obsolete 'NationalPrimaryCellPhoneNumber' column");

        await ApplyIsolatedSchemaChangeAsync(
            builder => builder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
                table.DropColumn("NationalPrimaryHomePhoneNumber")),
            "drop the obsolete 'NationalPrimaryHomePhoneNumber' column");
    }

    private async Task DropLegacyPhoneIndexTableAsync()
    {
        var dialect = Store.Configuration.SqlDialect;
        var table = $"{Store.Configuration.TablePrefix}{LegacyPhoneIndexTableName}";
        var quotedTable = dialect.QuoteForTableName(table, Store.Configuration.Schema);

        // The drop fails whenever the table is already gone, so it runs in isolation: on the shared migration
        // transaction a failed statement would otherwise take every sibling step down with it.
        var failure = await TryApplyIsolatedAsync(builder =>
            builder.Connection.ExecuteAsync($"drop table {quotedTable}", transaction: builder.Transaction));

        if (failure is null)
        {
            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation(
                    "Dropped the obsolete default-collection contact phone index table '{TableName}'.",
                    table);
            }

            return;
        }

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug(
                failure,
                "The obsolete default-collection contact phone index table '{TableName}' was not dropped because it was unavailable or already removed.",
                table);
        }
    }

    private async Task RemoveLegacyCollectionContactIndexTableAsync()
    {
        var dialect = Store.Configuration.SqlDialect;
        var tableName = Store.Configuration.TableNameConvention.GetIndexTable(typeof(OmnichannelContactIndex), OmnichannelConstants.CollectionName);
        var table = $"{Store.Configuration.TablePrefix}{tableName}";
        var quotedTable = dialect.QuoteForTableName(table, Store.Configuration.Schema);
        var rowCount = 0;

        // The count fails whenever the table is already gone, which is the usual case, so the count and the drop
        // run together in isolation: on the shared migration transaction a failed statement would otherwise take
        // every sibling step down with it.
        var failure = await TryApplyIsolatedAsync(async builder =>
        {
            rowCount = await builder.Connection.ExecuteScalarAsync<int>($"select count(*) from {quotedTable}", transaction: builder.Transaction);

            if (rowCount > 0)
            {
                return;
            }

            await builder.Connection.ExecuteAsync($"drop table {quotedTable}", transaction: builder.Transaction);
        });

        if (failure is not null)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug(failure, "The legacy Omnichannel collection contact index table '{TableName}' was not removed because it was not available for cleanup.", table);
            }

            return;
        }

        if (rowCount > 0)
        {
            Logger.LogWarning(
                "Skipping removal of the legacy Omnichannel collection contact index table because it still contains {RowCount} row(s).",
                rowCount);

            return;
        }

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation(
                "Dropped the legacy Omnichannel collection contact index table '{TableName}' so the default-collection contact index can be recreated.",
                table);
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
