using System.Data;
using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.DncRegistry.Migrations;

/// <summary>
/// Creates YesSql index tables for the local DNC registry feature.
/// </summary>
internal sealed class LocalDncRegistryMigrations : DataMigration
{
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
        if (!await IndexTableExistsAsync(typeof(LocalDncListIndex)))
        {
            await CreateLocalDncListIndexAsync();
        }

        if (!await IndexTableExistsAsync(typeof(LocalDncEntryIndex)))
        {
            await CreateLocalDncEntryIndexAsync();
        }

        return 3;
    }

    private async Task<bool> IndexTableExistsAsync(Type indexType)
    {
        var tableName = SchemaBuilder.TableNameConvention.GetIndexTable(indexType, DncRegistryConstants.CollectionName);

        if (SchemaBuilder.Connection.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await using var command = SchemaBuilder.Connection.CreateCommand();
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @tableName";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);
            var result = await command.ExecuteScalarAsync();

            return Convert.ToInt32(result) > 0;
        }

        var tables = SchemaBuilder.Connection.GetSchema("Tables");

        return tables.Rows
            .Cast<DataRow>()
            .Any(row => string.Equals(row["TABLE_NAME"]?.ToString(), tableName, StringComparison.OrdinalIgnoreCase));
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
