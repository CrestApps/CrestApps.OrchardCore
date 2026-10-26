using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Transactions.Migrations;

/// <summary>
/// Creates the index table that backs the provider-agnostic transaction ledger.
/// </summary>
internal sealed class TransactionMigrations : DataMigration
{
    /// <summary>
    /// Creates the initial schema.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<TransactionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Title")
            .Column<string>("Source")
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<int>("OwnerKind")
            .Column<string>("ReferenceType")
            .Column<string>("ReferenceId", column => column.WithLength(26))
            .Column<string>("ParentReferenceType", column => column.Nullable().WithLength(TransactionIndexProvider.MaxParentReferenceLength))
            .Column<string>("ParentReferenceId", column => column.Nullable().WithLength(TransactionIndexProvider.MaxParentReferenceLength))
            .Column<string>("CheckoutSessionId", column => column.WithLength(26))
            .Column<string>("ObligationId")
            .Column<string>("Currency", column => column.WithLength(8))
            .Column<decimal>("TotalAmount")
            .Column<decimal>("AmountPaid")
            .Column<decimal>("OutstandingAmount")
            .Column<TransactionStatus>("Status")
            .Column<DateTime>("DueUtc", column => column.Nullable())
            .Column<DateTime>("NextCycleUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("UpdatedUtc"),
            collection: TransactionsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table =>
        {
            table.CreateIndex("IDX_TransactionIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TransactionIndex_Owner", "OwnerId", "Status");
            table.CreateIndex("IDX_TransactionIndex_Status", "Status", "DueUtc");
            table.CreateIndex("IDX_TransactionIndex_Obligation", "CheckoutSessionId", "ObligationId");
            table.CreateIndex("IDX_TransactionIndex_Reference", "ReferenceType", "ReferenceId");
            table.CreateIndex("IDX_TransactionIndex_ParentReference", "ParentReferenceType", "ParentReferenceId");
            table.CreateIndex("IDX_TransactionIndex_Outstanding", "Status", "OutstandingAmount");
            table.CreateIndex("IDX_TransactionIndex_NextCycle", "NextCycleUtc");
        }, collection: TransactionsConstants.CollectionName);

        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the index reports read transactions through, newest first by <c>CreatedUtc</c> a page at a time, so each
    /// page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<TransactionIndex>(
            SchemaBuilder,
            "IDX_TransactionIndex_Report",
            TransactionsConstants.CollectionName,
            "CreatedUtc", "Id");
    }
}
