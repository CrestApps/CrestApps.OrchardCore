using System.Data;
using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.DncRegistry.Migrations;

/// <summary>
/// Creates YesSql index tables for the local DNC registry feature.
/// </summary>
internal sealed class LocalDncRegistryMigrations : DataMigration
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDncRegistryMigrations"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public LocalDncRegistryMigrations(ILogger<LocalDncRegistryMigrations> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates the initial index tables.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await CreateLocalDncListIndexAsync();
        await CreateLocalDncEntryIndexAsync();

        return 3;
    }

    /// <summary>
    /// Repairs installations created by the original version of the local DNC migration.
    /// </summary>
    public Task<int> UpdateFrom1Async()
    {
        return RepairIndexTablesAsync();
    }

    /// <summary>
    /// Repairs installations whose migration record advanced without the local DNC index tables being present.
    /// </summary>
    public Task<int> UpdateFrom2Async()
    {
        return RepairIndexTablesAsync();
    }

    private async Task<int> RepairIndexTablesAsync()
    {
        await EnsureIndexTableAsync(typeof(LocalDncListIndex), CreateLocalDncListIndexAsync);
        await EnsureIndexTableAsync(typeof(LocalDncEntryIndex), CreateLocalDncEntryIndexAsync);

        return 3;
    }

    private async Task EnsureIndexTableAsync(Type indexType, Func<Task> createAsync)
    {
        if (await IndexTableExistsAsync(indexType))
        {
            return;
        }

        try
        {
            await createAsync();
        }
        catch (Exception ex)
        {
            // The table already exists even though the schema lookup did not report it. The
            // create must not rethrow because an uncaught migration exception cancels the shared
            // migration session, which would silently roll back every other feature's migration.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "The local DNC index table for '{IndexType}' was not created because it was already present.", indexType.Name);
            }
        }
    }

    private async Task<bool> IndexTableExistsAsync(Type indexType)
    {
        var tableName = SchemaBuilder.TableNameConvention.GetIndexTable(indexType, DncRegistryConstants.CollectionName);

        // YesSql stores each tenant's tables with the shell's table prefix, so the physical
        // table name must be resolved before comparing against the database schema. Comparing
        // the unprefixed name would always report the table as missing on prefixed (for example
        // multi-tenant) installations and cause a duplicate-object error when it is recreated.
        var physicalTableName = $"{SchemaBuilder.TablePrefix}{tableName}";

        if (SchemaBuilder.Connection.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await using var command = SchemaBuilder.Connection.CreateCommand();
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @tableName";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = physicalTableName;
            command.Parameters.Add(parameter);
            var result = await command.ExecuteScalarAsync();

            return Convert.ToInt32(result) > 0;
        }

        var tables = SchemaBuilder.Connection.GetSchema("Tables");

        return tables.Rows
            .Cast<DataRow>()
            .Any(row => string.Equals(row["TABLE_NAME"]?.ToString(), physicalTableName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task CreateLocalDncListIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<LocalDncListIndex>(table => table
            .Column<string>("ListId", column => column.WithLength(26))
            .Column<string>("CountryCode", column => column.WithLength(2))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<LocalDncListStatus>("Status")
            .Column<DateTime>("CreatedUtc"),
            collection: DncRegistryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<LocalDncListIndex>(table => table
            .CreateIndex("IDX_DNC_LocalDncListIndex_DocumentId",
                "DocumentId",
                "ListId",
                "CountryCode"
            ),
            collection: DncRegistryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<LocalDncListIndex>(table => table
            .CreateIndex("IDX_DNC_LocalDncListIndex_Status_CreatedUtc",
                "Status",
                "CreatedUtc"),
            collection: DncRegistryConstants.CollectionName
        );
    }

    private async Task CreateLocalDncEntryIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<LocalDncEntryIndex>(table => table
            .Column<string>("EntryId", column => column.WithLength(26))
            .Column<string>("ListId", column => column.WithLength(26))
            .Column<string>("CountryCode", column => column.WithLength(2))
            .Column<string>("PhoneNumber", column => column.WithLength(30)),
            collection: DncRegistryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<LocalDncEntryIndex>(table => table
            .CreateIndex("IDX_DNC_LocalDncEntryIndex_DocumentId",
                "DocumentId",
                "ListId",
                "CountryCode",
                "PhoneNumber"
            ),
            collection: DncRegistryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<LocalDncEntryIndex>(table => table
            .CreateIndex("IDX_DNC_LocalDncEntryIndex_PhoneNumber_Country",
                "PhoneNumber",
                "CountryCode"
            ),
            collection: DncRegistryConstants.CollectionName
        );
    }
}
