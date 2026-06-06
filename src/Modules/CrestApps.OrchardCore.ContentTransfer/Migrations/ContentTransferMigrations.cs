using CrestApps.OrchardCore.ContentTransfer.Indexes;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContentTransfer.Migrations;

public sealed class ContentTransferMigrations : DataMigration
{
    private readonly IStore _store;
    private readonly IDbConnectionAccessor _dbConnectionAccessor;

    public ContentTransferMigrations(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor)
    {
        _store = store;
        _dbConnectionAccessor = dbConnectionAccessor;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContentTransferEntryIndex>(table => table
            .Column<string>("EntryId", column => column.WithLength(26))
            .Column<string>("Status", column => column.NotNull().WithLength(25))
            .Column<ContentTransferDirection>("Direction")
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<string>("Owner", column => column.WithLength(26))
        );

        await SchemaBuilder.AlterIndexTableAsync<ContentTransferEntryIndex>(table => table
            .CreateIndex("IDX_ContentTransferEntryIndex_DocumentId",
                "DocumentId",
                "EntryId",
                "Status",
                "CreatedUtc",
                "ContentType",
                "Owner")
        );

        await SchemaBuilder.AlterIndexTableAsync<ContentTransferEntryIndex>(table => table
            .CreateIndex("IDX_ContentTransferEntryIndex_Status",
                "Status",
                "CreatedUtc",
                "DocumentId",
                "ContentType",
                "Owner")
        );

        await SchemaBuilder.AlterIndexTableAsync<ContentTransferEntryIndex>(table => table
            .CreateIndex("IDX_ContentTransferEntryIndex_Direction",
                "Direction",
                "Status",
                "CreatedUtc",
                "DocumentId")
        );

        return 2;
    }
}
