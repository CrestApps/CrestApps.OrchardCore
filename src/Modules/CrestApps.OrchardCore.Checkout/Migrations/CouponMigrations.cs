using CrestApps.OrchardCore.Transactions.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;

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

    /// <summary>
    /// Adds the ledger of coupon uses: held while a checkout pays, redeemed when it completes, or given back.
    /// </summary>
    /// <returns>The schema version this migration upgrades to.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CouponRedemptionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("NormalizedCode", column => column.WithLength(64))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("CustomerId", column => column.WithLength(64))
            .Column<string>("NormalizedEmail", column => column.WithLength(255))
            .Column<string>("Status", column => column.WithLength(16))
            .Column<bool>("IsCounted")
            .Column<DateTime>("ExpiresUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: CheckoutConstants.CouponCollectionName
        );

        // Holding a use counts the code's live holds; redeeming finds the checkout's hold; a limit per customer counts
        // the customer's uses.
        await IndexTableIndexCreation.CreateIfMissingAsync<CouponRedemptionIndex>(SchemaBuilder, "IDX_CouponRedemptionIndex_Code", CheckoutConstants.CouponCollectionName, "NormalizedCode", "Status", "ExpiresUtc");
        await IndexTableIndexCreation.CreateIfMissingAsync<CouponRedemptionIndex>(SchemaBuilder, "IDX_CouponRedemptionIndex_Session", CheckoutConstants.CouponCollectionName, "SessionId", "Status");
        await IndexTableIndexCreation.CreateIfMissingAsync<CouponRedemptionIndex>(SchemaBuilder, "IDX_CouponRedemptionIndex_Customer", CheckoutConstants.CouponCollectionName, "NormalizedCode", "CustomerId");
        await IndexTableIndexCreation.CreateIfMissingAsync<CouponRedemptionIndex>(SchemaBuilder, "IDX_CouponRedemptionIndex_Email", CheckoutConstants.CouponCollectionName, "NormalizedCode", "NormalizedEmail");

        return 2;
    }
}
