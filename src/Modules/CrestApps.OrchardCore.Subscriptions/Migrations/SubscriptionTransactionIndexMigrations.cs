using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Creates and updates the YesSql map index table for subscription payment transactions.
/// </summary>
public sealed class SubscriptionTransactionIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the subscription transaction index table and its owner/date lookup index.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SubscriptionTransactionIndex>(table => table
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc")
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(20))
            .Column<double>("Amount")
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<string>("GatewayId", column => column.WithLength(50))
            .Column<string>("GatewayMode", column => column.WithLength(50))
            .Column<string>("GatewayTransactionId", column => column.WithLength(64))
            .Column<string>("ContentType", column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
        );

        await SchemaBuilder.AlterIndexTableAsync<SubscriptionTransactionIndex>(table => table
            .CreateIndex("IDX_SubscriptionTransactionIndex_DocumentId",
                "DocumentId",
                "OwnerId",
                "CreatedUtc")
        );

        return 1;
    }

    /// <summary>
    /// Adds the tax amount column to the subscription transaction index table.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<SubscriptionTransactionIndex>(table => table
            .AddColumn<double>("TaxAmount")
        );

        return 2;
    }
}
