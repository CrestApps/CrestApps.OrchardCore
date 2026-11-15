using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Invoicing.Migrations;

/// <summary>
/// Creates the usage meter and usage record indexes of the Billing feature in the <c>Invoicing</c> collection.
/// </summary>
public sealed class UsageMigrations : DataMigration
{
    /// <summary>
    /// Creates the index tables and their indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<UsageMeterIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(InvoiceIndexProvider.IdLength))
            .Column<string>("Name", column => column.Nullable().WithLength(InvoiceIndexProvider.TextLength))
            .Column<string>("Code", column => column.Nullable().WithLength(64))
            .Column<bool>("Enabled"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<UsageMeterIndex>(table => table
            .CreateIndex("IDX_UsageMeterIndex_ItemId", "ItemId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<UsageMeterIndex>(table => table
            .CreateIndex("IDX_UsageMeterIndex_Code", "Code", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.CreateMapIndexTableAsync<UsageRecordIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(InvoiceIndexProvider.IdLength))
            .Column<string>("MeterId", column => column.Nullable().WithLength(InvoiceIndexProvider.IdLength))
            .Column<string>("PartyKind", column => column.Nullable().WithLength(InvoiceIndexProvider.ShortLength))
            .Column<string>("PartyId", column => column.Nullable().WithLength(InvoiceIndexProvider.PartyIdLength))
            .Column<string>("EventId", column => column.Nullable().WithLength(InvoiceIndexProvider.IdLength * 2))
            .Column<decimal>("Quantity", column => column.WithPrecision(19).WithScale(6))
            .Column<DateTime>("OccurredUtc")
            .Column<DateTime>("CreatedUtc"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<UsageRecordIndex>(table => table
            .CreateIndex("IDX_UsageRecordIndex_ItemId", "ItemId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<UsageRecordIndex>(table => table
            .CreateIndex("IDX_UsageRecordIndex_Event", "MeterId", "EventId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<UsageRecordIndex>(table => table
            .CreateIndex("IDX_UsageRecordIndex_Period", "MeterId", "PartyKind", "PartyId", "OccurredUtc", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        return 1;
    }
}
