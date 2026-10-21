using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Creates the index installment plans are listed, searched and swept by.
/// </summary>
internal sealed class InstallmentPlanMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
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
            .Column<DateTime>("CreatedUtc")
            .Column<string>("ReferenceType", column => column.Nullable().WithLength(InstallmentPlanIndexProvider.ReferenceMaxLength))
            .Column<string>("ReferenceId", column => column.Nullable().WithLength(InstallmentPlanIndexProvider.ReferenceMaxLength))
            .Column<string>("SourceTransactionId", column => column.Nullable().WithLength(26)),
            collection: SubscriptionConstants.InstallmentPlanCollectionName
        );

        // The sweep reads the open plans every quarter hour, so it must never read the whole table. A plan is also found
        // from the record it pays for and from the transaction it replaces.
        await SchemaBuilder.AlterIndexTableAsync<InstallmentPlanIndex>(table =>
        {
            table.CreateIndex("IDX_InstallmentPlanIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_InstallmentPlanIndex_Status", "Status", "NextDueUtc");
            table.CreateIndex("IDX_InstallmentPlanIndex_Owner", "OwnerId");
            table.CreateIndex("IDX_InstallmentPlanIndex_Reference", "ReferenceType", "ReferenceId");
            table.CreateIndex("IDX_InstallmentPlanIndex_Source", "SourceTransactionId");
        }, collection: SubscriptionConstants.InstallmentPlanCollectionName);

        return 1;
    }
}
