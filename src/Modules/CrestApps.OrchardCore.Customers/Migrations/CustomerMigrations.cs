using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Customers.Migrations;

public sealed class CustomerMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CustomerIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("NormalizedEmail", column => column.WithLength(255))
            .Column<string>("NormalizedName", column => column.WithLength(255))
            .Column<string>("NormalizedCompany", column => column.WithLength(255))
            .Column<int>("Kind")
            .Column<int>("Status")
            .Column<bool>("HasAccount")
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("LastActivityUtc"),
            collection: CustomersConstants.CollectionName
        );

        foreach (var (name, columns) in new[]
        {
            ("IDX_CustomerIndex_ItemId", "ItemId"),
            ("IDX_CustomerIndex_UserId", "UserId"),
            ("IDX_CustomerIndex_NormalizedEmail", "NormalizedEmail"),
            ("IDX_CustomerIndex_NormalizedName", "NormalizedName"),
            ("IDX_CustomerIndex_Status_LastActivityUtc", "Status,LastActivityUtc"),
        })
        {
            await SchemaBuilder.AlterIndexTableAsync<CustomerIndex>(table => table
                .CreateIndex(name, columns.Split(',')),
                collection: CustomersConstants.CollectionName
            );
        }

        return 1;
    }
}
