using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Creates the index installment plans are listed, searched and swept by.
/// </summary>
public sealed class InstallmentPlanMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<InstallmentPlanIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Title", column => column.WithLength(255))
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<string>("CustomerName", column => column.WithLength(255))
            .Column<string>("CustomerEmail", column => column.WithLength(255))
            .Column<InstallmentPlanStatus>("Status")
            .Column<string>("Currency", column => column.WithLength(8))
            .Column<decimal>("TotalAmount")
            .Column<DateTime>("NextDueUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: SubscriptionConstants.InstallmentPlanCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InstallmentPlanIndex>(table => table
            .CreateIndex("IDX_InstallmentPlanIndex_ItemId", "ItemId"),
            collection: SubscriptionConstants.InstallmentPlanCollectionName
        );

        // The sweep reads the open plans every quarter hour, so it must never read the whole table.
        await SchemaBuilder.AlterIndexTableAsync<InstallmentPlanIndex>(table => table
            .CreateIndex("IDX_InstallmentPlanIndex_Status", "Status", "NextDueUtc"),
            collection: SubscriptionConstants.InstallmentPlanCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InstallmentPlanIndex>(table => table
            .CreateIndex("IDX_InstallmentPlanIndex_Owner", "OwnerId"),
            collection: SubscriptionConstants.InstallmentPlanCollectionName
        );

        return 1;
    }
}
