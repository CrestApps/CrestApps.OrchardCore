using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Invoicing.Migrations;

/// <summary>
/// Creates the recurring plan index of the Recurring feature in the <c>Invoicing</c> collection.
/// </summary>
public sealed class RecurringPlansMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table and its indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<RecurringPlanIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(InvoiceIndexProvider.IdLength))
            .Column<string>("Name", column => column.Nullable().WithLength(InvoiceIndexProvider.TextLength))
            .Column<int>("Status")
            .Column<string>("BillToName", column => column.Nullable().WithLength(InvoiceIndexProvider.TextLength))
            .Column<string>("Reference", column => column.Nullable().WithLength(InvoiceIndexProvider.TextLength))
            .Column<string>("Currency", column => column.Nullable().WithLength(3))
            .Column<decimal>("Total")
            .Column<DateTime>("NextRunDate", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<RecurringPlanIndex>(table => table
            .CreateIndex("IDX_RecurringPlanIndex_ItemId", "ItemId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<RecurringPlanIndex>(table => table
            .CreateIndex("IDX_RecurringPlanIndex_Due", "Status", "NextRunDate", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<RecurringPlanIndex>(table => table
            .CreateIndex("IDX_RecurringPlanIndex_Created", "CreatedUtc", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        return 1;
    }
}
