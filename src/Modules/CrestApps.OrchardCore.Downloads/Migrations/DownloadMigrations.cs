using CrestApps.OrchardCore.Downloads.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Downloads.Migrations;

public sealed class DownloadMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public DownloadMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(DownloadsConstants.Parts.DigitalFilesPart, part => part
            .Attachable()
            .WithDisplayName("Digital files")
            .WithDescription("The files a buyer of the product downloads, kept privately, with how often and how long they can."));

        await SchemaBuilder.CreateMapIndexTableAsync<DownloadEntitlementIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<string>("FileId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc"),
            collection: DownloadsConstants.CollectionName
        );

        // Safe to run again: a server that finished the step first has already made them.
        await IndexTableIndexCreation.CreateIfMissingAsync<DownloadEntitlementIndex>(SchemaBuilder, "IDX_DownloadEntitlementIndex_ItemId", DownloadsConstants.CollectionName, "ItemId");
        await IndexTableIndexCreation.CreateIfMissingAsync<DownloadEntitlementIndex>(SchemaBuilder, "IDX_DownloadEntitlementIndex_OrderId", DownloadsConstants.CollectionName, "OrderId", "CreatedUtc");

        return 1;
    }
}
