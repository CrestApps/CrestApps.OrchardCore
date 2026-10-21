using CrestApps.OrchardCore.Reviews.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Reviews.Migrations;

public sealed class ReviewMigrations : DataMigration
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

        // Safe to run again: a server that finished the step first has already made them.
        await IndexTableIndexCreation.CreateIfMissingAsync<ProductReviewIndex>(SchemaBuilder, "IDX_ProductReviewIndex_ItemId", ReviewsConstants.CollectionName, "ItemId");
        await IndexTableIndexCreation.CreateIfMissingAsync<ProductReviewIndex>(SchemaBuilder, "IDX_ProductReviewIndex_Product", ReviewsConstants.CollectionName, "ProductId", "Status");
        await IndexTableIndexCreation.CreateIfMissingAsync<ProductReviewIndex>(SchemaBuilder, "IDX_ProductReviewIndex_Status", ReviewsConstants.CollectionName, "Status", "CreatedUtc");

        return 1;
    }
}
