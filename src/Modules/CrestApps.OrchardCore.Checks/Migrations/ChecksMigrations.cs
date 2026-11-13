using CrestApps.OrchardCore.Checks.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Checks.Migrations;

internal sealed class ChecksMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CheckRecordIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("BankAccountId", column => column.WithLength(26))
            .Column<long>("Number")
            .Column<string>("Status", column => column.WithLength(30))
            .Column<string>("PayeeName", column => column.WithLength(255))
            .Column<decimal>("Amount", column => column.WithPrecision(19).WithScale(4))
            .Column<string>("Currency", column => column.WithLength(3))
            .Column<DateTime>("CheckDate")
            .Column<string>("SourceKind", column => column.WithLength(100))
            .Column<string>("SourceId", column => column.WithLength(255))
            .Column<DateTime>("IssuedUtc"),
            collection: ChecksConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<CheckRecordIndex>(table => table
            .CreateIndex("IDX_CheckRecordIndex_ItemId", "ItemId", "DocumentId"),
            collection: ChecksConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<CheckRecordIndex>(table => table
            .CreateIndex("IDX_CheckRecordIndex_Number", "BankAccountId", "Number", "DocumentId"),
            collection: ChecksConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<CheckRecordIndex>(table => table
            .CreateIndex("IDX_CheckRecordIndex_Source", "SourceKind", "SourceId", "DocumentId"),
            collection: ChecksConstants.CollectionName);

        return 1;
    }
}
