using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Transactions.Migrations;

/// <summary>
/// Creates the index table that backs the provider-agnostic transaction ledger.
/// </summary>
public sealed class TransactionMigrations : DataMigration
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
            .Column<string>("ReferenceType")
            .Column<string>("ReferenceId", column => column.WithLength(26))
            .Column<string>("CheckoutSessionId", column => column.WithLength(26))
            .Column<string>("ObligationId")
            .Column<string>("Currency", column => column.WithLength(8))
            .Column<decimal>("TotalAmount")
            .Column<decimal>("AmountPaid")
            .Column<TransactionStatus>("Status")
            .Column<DateTime>("DueUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("UpdatedUtc"),
            collection: TransactionsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table => table
            .CreateIndex("IDX_TransactionIndex_ItemId", "ItemId"),
            collection: TransactionsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table => table
            .CreateIndex("IDX_TransactionIndex_Owner", "OwnerId", "Status"),
            collection: TransactionsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table => table
            .CreateIndex("IDX_TransactionIndex_Status", "Status", "DueUtc"),
            collection: TransactionsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table => table
            .CreateIndex("IDX_TransactionIndex_Obligation", "CheckoutSessionId", "ObligationId"),
            collection: TransactionsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table => table
            .CreateIndex("IDX_TransactionIndex_Reference", "ReferenceType", "ReferenceId"),
            collection: TransactionsConstants.CollectionName
        );

        return 1;
    }
}
