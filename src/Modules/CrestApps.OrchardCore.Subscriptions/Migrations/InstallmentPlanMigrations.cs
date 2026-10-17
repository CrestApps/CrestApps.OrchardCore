using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
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

        await CreateReferenceIndexesAsync();

        return 2;
    }

    /// <summary>
    /// Adds the columns a plan is found by from the record it pays for and from the transaction it replaces. Plans
    /// created before them pay for no other record and replace no transaction, so the new columns stay empty for them.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<InstallmentPlanIndex>(table => table
            .AddColumn<string>("ReferenceType", column => column.Nullable().WithLength(InstallmentPlanIndexProvider.ReferenceMaxLength)),
            collection: SubscriptionConstants.InstallmentPlanCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InstallmentPlanIndex>(table => table
            .AddColumn<string>("ReferenceId", column => column.Nullable().WithLength(InstallmentPlanIndexProvider.ReferenceMaxLength)),
            collection: SubscriptionConstants.InstallmentPlanCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InstallmentPlanIndex>(table => table
            .AddColumn<string>("SourceTransactionId", column => column.Nullable().WithLength(26)),
            collection: SubscriptionConstants.InstallmentPlanCollectionName
        );

        await CreateReferenceIndexesAsync();

        return 2;
    }

    private async Task CreateReferenceIndexesAsync()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<InstallmentPlanIndex>(
            SchemaBuilder,
            "IDX_InstallmentPlanIndex_Reference",
            SubscriptionConstants.InstallmentPlanCollectionName,
            "ReferenceType", "ReferenceId");

        await IndexTableIndexCreation.CreateIfMissingAsync<InstallmentPlanIndex>(
            SchemaBuilder,
            "IDX_InstallmentPlanIndex_Source",
            SubscriptionConstants.InstallmentPlanCollectionName,
            "SourceTransactionId");
    }
}
