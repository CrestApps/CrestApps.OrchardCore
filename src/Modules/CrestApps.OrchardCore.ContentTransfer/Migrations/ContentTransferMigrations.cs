using CrestApps.OrchardCore.ContentTransfer.Indexes;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContentTransfer.Migrations;

public sealed class ContentTransferMigrations : DataMigration
{
    private readonly ILogger _logger;

    public ContentTransferMigrations(ILogger<ContentTransferMigrations> logger)
    {
        _logger = logger;
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

    public async Task<int> UpdateFrom1Async()
    {
        try
        {
            await SchemaBuilder.AlterIndexTableAsync<ContentTransferEntryIndex>(table =>
                table.AddColumn<ContentTransferDirection>("Direction"));

            await SchemaBuilder.AlterIndexTableAsync<ContentTransferEntryIndex>(table => table
                .CreateIndex("IDX_ContentTransferEntryIndex_Direction",
                    "Direction",
                    "Status",
                    "CreatedUtc",
                    "DocumentId")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating the ContentTransferEntryIndex table.");
        }

        return 2;
    }
}
