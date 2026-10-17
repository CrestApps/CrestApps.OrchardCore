using CrestApps.OrchardCore.Transactions.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Transactions.Migrations;

/// <summary>
/// Creates the index that finds the transactions belonging to a record through their parent reference.
/// </summary>
/// <remarks>
/// Transactions written before this index existed carry no parent reference, so there is nothing to backfill.
/// </remarks>
public sealed class TransactionParentReferenceMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<TransactionParentReferenceIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ParentReferenceType", column => column.WithLength(TransactionParentReferenceIndexProvider.MaxLength))
            .Column<string>("ParentReferenceId", column => column.WithLength(TransactionParentReferenceIndexProvider.MaxLength)),
            collection: TransactionsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TransactionParentReferenceIndex>(table => table
            .CreateIndex("IDX_TransactionParentRef_Reference", "ParentReferenceType", "ParentReferenceId"),
            collection: TransactionsConstants.CollectionName
        );

        return 1;
    }
}
