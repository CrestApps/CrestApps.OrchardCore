using CrestApps.OrchardCore.Addresses.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Addresses.Migrations;

/// <summary>
/// Creates the YesSql map index table and database indexes for country content items.
/// </summary>
public sealed class CountryIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the country index table and its lookup indexes.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CountryIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("Code", column => column.WithLength(2))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<bool>("Published")
            .Column<bool>("Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<CountryIndex>(table => table
            .CreateIndex("IDX_CountryIndex_DocumentId",
                "DocumentId",
                "ContentItemId",
                "Code",
                "Published",
                "Latest")
        );

        return 1;
    }
}
