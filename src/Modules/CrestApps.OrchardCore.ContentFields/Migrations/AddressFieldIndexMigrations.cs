using CrestApps.OrchardCore.ContentFields.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContentFields.Migrations;

public sealed class AddressFieldIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AddressFieldIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<string>("ContentPart", column => column.WithLength(255))
            .Column<string>("ContentField", column => column.WithLength(255))
            .Column<bool>("Published", column => column.Nullable())
            .Column<bool>("Latest", column => column.Nullable())
            .Column<string>("Country", column => column.Nullable().WithLength(2))
            .Column<string>("Region", column => column.Nullable().WithLength(100))
            .Column<string>("City", column => column.Nullable().WithLength(255))
            .Column<string>("PostalCode", column => column.Nullable().WithLength(50))
        );

        await SchemaBuilder.AlterIndexTableAsync<AddressFieldIndex>(table => table
            .CreateIndex("IDX_AddressFieldIndex_Place",
                "DocumentId",
                "Country",
                "Region",
                "City",
                "Published",
                "Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<AddressFieldIndex>(table => table
            .CreateIndex("IDX_AddressFieldIndex_PostalCode",
                "DocumentId",
                "Country",
                "PostalCode",
                "Published",
                "Latest")
        );

        return 1;
    }
}
