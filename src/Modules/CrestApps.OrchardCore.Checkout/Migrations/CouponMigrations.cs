using CrestApps.OrchardCore.Checkout.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Checkout.Migrations;

/// <summary>
/// Creates the index table that backs the coupon catalog.
/// </summary>
public sealed class CouponMigrations : DataMigration
{
    /// <summary>
    /// Creates the initial schema.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CouponIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("NormalizedCode", column => column.WithLength(64))
            .Column<bool>("IsEnabled")
            .Column<DateTime>("EndsUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: CheckoutConstants.CouponCollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CouponIndex>(table => table
            .CreateIndex("IDX_CouponIndex_ItemId", "ItemId"),
            collection: CheckoutConstants.CouponCollectionName
        );

        // Every checkout that carries a code looks it up on every step, so this must never be a scan.
        await SchemaBuilder.AlterIndexTableAsync<CouponIndex>(table => table
            .CreateIndex("IDX_CouponIndex_Code", "NormalizedCode"),
            collection: CheckoutConstants.CouponCollectionName
        );

        return 1;
    }
}
