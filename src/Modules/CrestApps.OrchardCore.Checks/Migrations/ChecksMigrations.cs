using CrestApps.OrchardCore.Checks.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Checks.Migrations;

internal sealed class ChecksMigrations : DataMigration
{
    private readonly IStore _store;

    public ChecksMigrations(IStore store)
    {
        _store = store;
    }

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

    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<CheckRecordIndex>(table => table
            .AddColumn<string>("AccessKey", column => column.Nullable().WithLength(255)),
            collection: ChecksConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<CheckRecordIndex>(table => table
            .AddColumn<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ChecksConstants.CollectionName);

        // Checks written before the column changed last when they were issued, as far as the positive pay file knows.
        var table = IndexMigrationSql.GetQuotedTableName(SchemaBuilder, _store, typeof(CheckRecordIndex), ChecksConstants.CollectionName);
        var modified = SchemaBuilder.Dialect.QuoteForColumnName("ModifiedUtc");
        var issued = SchemaBuilder.Dialect.QuoteForColumnName("IssuedUtc");

        await using (var command = SchemaBuilder.Connection.CreateCommand())
        {
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText = $"UPDATE {table} SET {modified} = {issued} WHERE {modified} IS NULL";

            await command.ExecuteNonQueryAsync();
        }

        await SchemaBuilder.AlterIndexTableAsync<CheckRecordIndex>(table => table
            .CreateIndex("IDX_CheckRecordIndex_Modified", "ModifiedUtc", "DocumentId"),
            collection: ChecksConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<CheckRecordIndex>(table => table
            .CreateIndex("IDX_CheckRecordIndex_Access", "SourceKind", "AccessKey", "DocumentId"),
            collection: ChecksConstants.CollectionName);

        return 2;
    }
}
