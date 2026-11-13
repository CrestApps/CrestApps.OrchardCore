using CrestApps.OrchardCore.Disbursements.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Disbursements.Migrations;

internal sealed class DisbursementsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<DisbursementIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(30))
            .Column<string>("Method", column => column.WithLength(50))
            .Column<string>("Provider", column => column.Nullable().WithLength(50))
            .Column<string>("PayeeName", column => column.Nullable().WithLength(255))
            .Column<string>("Currency", column => column.WithLength(3))
            .Column<string>("SourceBankAccountId", column => column.Nullable().WithLength(26))
            .Column<string>("DestinationBankAccountId", column => column.Nullable().WithLength(26))
            .Column<string>("SourceKind", column => column.Nullable().WithLength(100))
            .Column<string>("SourceId", column => column.Nullable().WithLength(255))
            .Column<string>("AccessKey", column => column.Nullable().WithLength(255))
            .Column<string>("IdempotencyKey", column => column.WithLength(255))
            .Column<string>("TraceNumber", column => column.Nullable().WithLength(30))
            .Column<string>("CheckId", column => column.Nullable().WithLength(26))
            .Column<string>("BatchId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("ModifiedUtc"),
            collection: DisbursementsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<DisbursementIndex>(table => table
            .CreateIndex("IDX_DisbursementIndex_ItemId", "ItemId", "DocumentId"),
            collection: DisbursementsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<DisbursementIndex>(table => table
            .CreateIndex("IDX_DisbursementIndex_Key", "IdempotencyKey", "DocumentId"),
            collection: DisbursementsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<DisbursementIndex>(table => table
            .CreateIndex("IDX_DisbursementIndex_Trace", "TraceNumber", "DocumentId"),
            collection: DisbursementsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<DisbursementIndex>(table => table
            .CreateIndex("IDX_DisbursementIndex_Source", "SourceKind", "SourceId", "DocumentId"),
            collection: DisbursementsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<DisbursementIndex>(table => table
            .CreateIndex("IDX_DisbursementIndex_Access", "SourceKind", "AccessKey", "DocumentId"),
            collection: DisbursementsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<DisbursementIndex>(table => table
            .CreateIndex("IDX_DisbursementIndex_Batch", "BatchId", "DocumentId"),
            collection: DisbursementsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<DisbursementIndex>(table => table
            .CreateIndex("IDX_DisbursementIndex_Accounts", "SourceBankAccountId", "DestinationBankAccountId", "DocumentId"),
            collection: DisbursementsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<DisbursementIndex>(table => table
            .CreateIndex("IDX_DisbursementIndex_Modified", "ModifiedUtc", "DocumentId"),
            collection: DisbursementsConstants.CollectionName);

        await SchemaBuilder.CreateMapIndexTableAsync<DisbursementBatchIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Provider", column => column.WithLength(50))
            .Column<string>("Status", column => column.WithLength(30))
            .Column<string>("SourceBankAccountId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("EffectiveDate")
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("ReleasedUtc", column => column.Nullable()),
            collection: DisbursementsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<DisbursementBatchIndex>(table => table
            .CreateIndex("IDX_DisbursementBatchIndex_ItemId", "ItemId", "DocumentId"),
            collection: DisbursementsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<DisbursementBatchIndex>(table => table
            .CreateIndex("IDX_DisbursementBatchIndex_Released", "Status", "ReleasedUtc", "DocumentId"),
            collection: DisbursementsConstants.CollectionName);

        return 1;
    }
}
