using CrestApps.OrchardCore.Addresses.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Addresses.Migrations;

/// <summary>
/// Creates the YesSql map index table and database indexes for the shared geographic area index that covers
/// countries, regions, counties, cities, and districts.
/// </summary>
public sealed class GeographicAreaIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the geographic area index table and its lookup indexes.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<GeographicAreaIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(50))
            .Column<string>("Code", column => column.WithLength(50))
            .Column<string>("ParentContentItemId", column => column.WithLength(26))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<bool>("Published")
            .Column<bool>("Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<GeographicAreaIndex>(table => table
            .CreateIndex("IDX_GeographicAreaIndex_Code",
                "DocumentId",
                "ContentItemId",
                "ContentType",
                "Code",
                "Published",
                "Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<GeographicAreaIndex>(table => table
            .CreateIndex("IDX_GeographicAreaIndex_Parent",
                "DocumentId",
                "ContentType",
                "ParentContentItemId",
                "Published",
                "Latest")
        );

        return 1;
    }
}
