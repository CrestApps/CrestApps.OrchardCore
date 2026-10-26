using CrestApps.OrchardCore.Downloads.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Downloads.Migrations;

internal sealed class DownloadMigrations : DataMigration
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

        await SchemaBuilder.AlterIndexTableAsync<DownloadEntitlementIndex>(table =>
        {
            table.CreateIndex("IDX_DownloadEntitlementIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_DownloadEntitlementIndex_OrderId", "OrderId", "CreatedUtc");
        }, collection: DownloadsConstants.CollectionName);

        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the index reports read download entitlements through, newest first by <c>CreatedUtc</c> a page at a time,
    /// so each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<DownloadEntitlementIndex>(
            SchemaBuilder,
            "IDX_DownloadEntitlementIndex_Report",
            DownloadsConstants.CollectionName,
            "CreatedUtc", "Id");
    }
}
