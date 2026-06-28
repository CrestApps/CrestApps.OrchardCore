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
        await SchemaBuilder.CreateMapIndexTableAsync<LocalDncListIndex>(table => table
            .Column<string>("ListId", column => column.WithLength(26))
            .Column<string>("CountryCode", column => column.WithLength(2))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<LocalDncListStatus>("Status")
            .Column<DateTime>("CreatedUtc"),
            collection: DncRegistryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<LocalDncListIndex>(table => table
            .CreateIndex("IDX_LocalDncListIndex_DocumentId",
                "DocumentId",
                "ListId",
                "CountryCode"
            ),
            collection: DncRegistryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<LocalDncListIndex>(table => table
            .CreateIndex("IDX_LocalDncListIndex_Status_CreatedUtc",
                "Status",
                "CreatedUtc"),
            collection: DncRegistryConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<LocalDncEntryIndex>(table => table
            .Column<string>("EntryId", column => column.WithLength(26))
            .Column<string>("ListId", column => column.WithLength(26))
            .Column<string>("CountryCode", column => column.WithLength(2))
            .Column<string>("PhoneNumber", column => column.WithLength(30)),
            collection: DncRegistryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<LocalDncEntryIndex>(table => table
            .CreateIndex("IDX_LocalDncEntryIndex_DocumentId",
                "DocumentId",
                "ListId",
                "CountryCode",
                "PhoneNumber"
            ),
            collection: DncRegistryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<LocalDncEntryIndex>(table => table
            .CreateIndex("IDX_LocalDncEntryIndex_PhoneNumber_Country",
                "PhoneNumber",
                "CountryCode"
            ),
            collection: DncRegistryConstants.CollectionName
        );

        return 1;
    }
}
