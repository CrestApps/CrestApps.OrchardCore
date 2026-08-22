using CrestApps.OrchardCore.Customers.Models;
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
            .Column<int>("OwnerKind")
            .Column<string>("ReferenceType")
            .Column<string>("ReferenceId", column => column.WithLength(26))
            .Column<string>("CheckoutSessionId", column => column.WithLength(26))
            .Column<string>("ObligationId")
            .Column<string>("Currency", column => column.WithLength(8))
            .Column<decimal>("TotalAmount")
            .Column<decimal>("AmountPaid")
            .Column<decimal>("OutstandingAmount")
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

        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table => table
            .CreateIndex("IDX_TransactionIndex_Outstanding", "Status", "OutstandingAmount"),
            collection: TransactionsConstants.CollectionName
        );

        return 3;
    }

    /// <summary>
    /// Adds the owner-kind column so a guest obligation can be distinguished from an authenticated one.
    /// Existing rows keep the default value, which maps to <see cref="CustomerOwnerKind.Authenticated"/>.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table => table
            .AddColumn<int>("OwnerKind", column => column.WithDefault(0)),
            collection: TransactionsConstants.CollectionName
        );

        return 2;
    }

    /// <summary>
    /// Adds the outstanding-amount column so a report can query and sort by what is still owed without
    /// loading every transaction document. Existing rows default to zero and are backfilled the next time
    /// each transaction is written.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table => table
            .AddColumn<decimal>("OutstandingAmount", column => column.WithDefault(0)),
            collection: TransactionsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table => table
            .CreateIndex("IDX_TransactionIndex_Outstanding", "Status", "OutstandingAmount"),
            collection: TransactionsConstants.CollectionName
        );

        return 3;
    }
}
