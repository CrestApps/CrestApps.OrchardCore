using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Creates the YesSql map index table and database indexes for subscription content items.
/// </summary>
public sealed class SubscriptionsContentItemIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the subscription content item index table and its lookup indexes.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SubscriptionsContentItemIndex>(table => table
            .Column<string>("ContentType")
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<int>("Order")
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("ModifiedUtc")
            .Column<bool>("Published")
            .Column<bool>("Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionsContentItemIndex>(table => table
            .CreateIndex("IDX_SubscriptionsContentItemIndex_DocumentId",
                "DocumentId",
                "ContentType",
                "Published",
                "CreatedUtc",
                "Order",
                "Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionsContentItemIndex>(table => table
            .CreateIndex("IDX_SubscriptionsContentItemIndex_ContentItemId",
                "ContentItemId",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionsContentItemIndex>(table => table
            .CreateIndex("IDX_SubscriptionsContentItemIndex_ContentItemVersionId",
                "ContentItemVersionId",
                "DocumentId")
        );

        return 1;
    }
}
