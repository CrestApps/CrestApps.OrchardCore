using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Checkout.Migrations;

/// <summary>
/// Creates the index tables that back the coupon catalog, the ledger of coupon uses, and the unique codes generated
/// for a coupon.
/// </summary>
internal sealed class CouponMigrations : DataMigration
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
            .Column<bool>("IsAutomatic", column => column.WithDefault(false))
            .Column<DateTime>("EndsUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: CheckoutConstants.CouponCollectionName
        );

        // Every checkout that carries a code looks it up on every step, so this must never be a scan. The discounts
        // that apply by themselves, without a code, are found by every checkout.
        await SchemaBuilder.AlterIndexTableAsync<CouponIndex>(table =>
        {
            table.CreateIndex("IDX_CouponIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_CouponIndex_Code", "NormalizedCode");
            table.CreateIndex("IDX_CouponIndex_Automatic", "IsAutomatic", "IsEnabled");
        }, collection: CheckoutConstants.CouponCollectionName);

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
        await SchemaBuilder.AlterIndexTableAsync<CouponRedemptionIndex>(table =>
        {
            table.CreateIndex("IDX_CouponRedemptionIndex_Code", "NormalizedCode", "Status", "ExpiresUtc");
            table.CreateIndex("IDX_CouponRedemptionIndex_Session", "SessionId", "Status");
            table.CreateIndex("IDX_CouponRedemptionIndex_Customer", "NormalizedCode", "CustomerId");
            table.CreateIndex("IDX_CouponRedemptionIndex_Email", "NormalizedCode", "NormalizedEmail");
        }, collection: CheckoutConstants.CouponCollectionName);

        await SchemaBuilder.CreateMapIndexTableAsync<CouponCodeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CouponId", column => column.WithLength(26))
            .Column<string>("NormalizedCode", column => column.WithLength(64))
            .Column<int>("RedemptionCount")
            .Column<DateTime>("CreatedUtc")
            .Column<string>("IdempotencyKey", column => column.Nullable().WithLength(CheckoutConstants.MaxCouponIdempotencyKeyLength))
            .Column<string>("NormalizedEmail", column => column.Nullable().WithLength(EmailAddresses.MaxLength)),
            collection: CheckoutConstants.CouponCollectionName
        );

        // A code the buyer types is looked up here when it isn't a coupon's own; a coupon's page counts its codes.
        await SchemaBuilder.AlterIndexTableAsync<CouponCodeIndex>(table =>
        {
            table.CreateIndex("IDX_CouponCodeIndex_Code", "NormalizedCode");
            table.CreateIndex("IDX_CouponCodeIndex_Coupon", "CouponId", "RedemptionCount");
            table.CreateIndex("IDX_CouponCodeIndex_ItemId", "ItemId");
        }, collection: CheckoutConstants.CouponCollectionName);

        await CreateIdempotencyKeyIndexAsync();
        await CreateNormalizedEmailIndexAsync();
        await CreateReportIndexesAsync();

        return 4;
    }

    /// <summary>
    /// Adds the indexes reports read coupons and the uses of their codes through, newest first by <c>CreatedUtc</c> a
    /// page at a time, so each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexesAsync();

        return 2;
    }

    /// <summary>
    /// Adds the key a code issued to one person is found by, so asking to issue it again returns the same code.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await IndexTableColumnCreation.AddIfMissingAsync<CouponCodeIndex>(
            SchemaBuilder,
            CheckoutConstants.CouponCollectionName,
            "IdempotencyKey",
            table => table.AddColumn<string>("IdempotencyKey", column => column.Nullable().WithLength(CheckoutConstants.MaxCouponIdempotencyKeyLength)));

        await CreateIdempotencyKeyIndexAsync();

        return 3;
    }

    /// <summary>
    /// Adds the e-mail address a code is bound to, so a privacy request finds the codes issued to a person. No code was
    /// bound to an address before this step, so no row needs to be filled in.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await IndexTableColumnCreation.AddIfMissingAsync<CouponCodeIndex>(
            SchemaBuilder,
            CheckoutConstants.CouponCollectionName,
            "NormalizedEmail",
            table => table.AddColumn<string>("NormalizedEmail", column => column.Nullable().WithLength(EmailAddresses.MaxLength)));

        await CreateNormalizedEmailIndexAsync();

        return 4;
    }

    private Task<bool> CreateNormalizedEmailIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<CouponCodeIndex>(
            SchemaBuilder,
            "IDX_CouponCodeIndex_NormalizedEmail",
            CheckoutConstants.CouponCollectionName,
            "NormalizedEmail");
    }

    // Not unique: every code generated in bulk has no key, and SQL Server counts those NULLs as equal. The issuer's
    // lock on the key is what keeps one code per key.
    private Task<bool> CreateIdempotencyKeyIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<CouponCodeIndex>(
            SchemaBuilder,
            "IDX_CouponCodeIndex_IdempotencyKey",
            CheckoutConstants.CouponCollectionName,
            "IdempotencyKey");
    }

    private async Task CreateReportIndexesAsync()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<CouponIndex>(
            SchemaBuilder,
            "IDX_CouponIndex_Report",
            CheckoutConstants.CouponCollectionName,
            "CreatedUtc", "Id");

        await IndexTableIndexCreation.CreateIfMissingAsync<CouponRedemptionIndex>(
            SchemaBuilder,
            "IDX_CouponRedemptionIndex_Report",
            CheckoutConstants.CouponCollectionName,
            "CreatedUtc", "Id");
    }
}
