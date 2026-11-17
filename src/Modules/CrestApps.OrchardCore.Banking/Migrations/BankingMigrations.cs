using CrestApps.OrchardCore.Banking.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Banking.Migrations;

internal sealed class BankingMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<BankAccountIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("OwnerKind", column => column.WithLength(50))
            .Column<string>("OwnerId", column => column.WithLength(255))
            .Column<string>("Status", column => column.WithLength(30))
            .Column<string>("Country", column => column.WithLength(2))
            .Column<string>("Currency", column => column.WithLength(3))
            .Column<DateTime>("CreatedUtc"),
            collection: BankingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<BankAccountIndex>(table => table
            .CreateIndex("IDX_BankAccountIndex_ItemId", "ItemId", "DocumentId"),
            collection: BankingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<BankAccountIndex>(table => table
            .CreateIndex("IDX_BankAccountIndex_Owner", "OwnerKind", "OwnerId", "DocumentId"),
            collection: BankingConstants.CollectionName);

        return 1;
    }
}
