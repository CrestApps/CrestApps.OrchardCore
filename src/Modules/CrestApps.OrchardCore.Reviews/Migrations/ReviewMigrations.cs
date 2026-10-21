using CrestApps.OrchardCore.Reviews.Indexes;
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

        await SchemaBuilder.AlterIndexTableAsync<ProductReviewIndex>(table =>
        {
            table.CreateIndex("IDX_ProductReviewIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ProductReviewIndex_Product", "ProductId", "Status");
            table.CreateIndex("IDX_ProductReviewIndex_Status", "Status", "CreatedUtc");
        }, collection: ReviewsConstants.CollectionName);

        return 1;
    }
}
