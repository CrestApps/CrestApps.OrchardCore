using CrestApps.OrchardCore.Reviews.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Reviews.Migrations;

internal sealed class ReviewMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProductReviewIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProductId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<int>("Rating")
            .Column<DateTime>("CreatedUtc"),
            collection: ReviewsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductReviewIndex>(table =>
        {
            table.CreateIndex("IDX_ProductReviewIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ProductReviewIndex_Product", "ProductId", "Status");
            table.CreateIndex("IDX_ProductReviewIndex_Status", "Status", "CreatedUtc");
        }, collection: ReviewsConstants.CollectionName);

        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the index reports read reviews through, newest first by <c>CreatedUtc</c> a page at a time, so each page and
    /// each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<ProductReviewIndex>(
            SchemaBuilder,
            "IDX_ProductReviewIndex_Report",
            ReviewsConstants.CollectionName,
            "CreatedUtc", "Id");
    }
}
