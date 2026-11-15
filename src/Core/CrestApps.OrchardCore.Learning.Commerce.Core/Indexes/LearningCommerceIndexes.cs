using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Commerce.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.YesSql.Core.Ledgers;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Commerce.Core.Indexes;

public sealed class LearningPurchaseIndex : CatalogItemIndex
{
    public string OrderId { get; set; }

    public string LineId { get; set; }

    public string UserId { get; set; }
}

public sealed class LicensePoolIndex : CatalogItemIndex
{
    public string AccountId { get; set; }

    public string CourseId { get; set; }

    public string OrderId { get; set; }

    public string LineId { get; set; }

    public LicensePoolState State { get; set; }
}

public sealed class LicenseSeatIndex : CatalogItemIndex
{
    public string PoolId { get; set; }

    public string AccountId { get; set; }

    public string UserId { get; set; }

    public string NormalizedEmail { get; set; }

    public string InvitationId { get; set; }

    public LicenseSeatState State { get; set; }
}

public sealed class LearningPurchaseIndexProvider : IndexProvider<LearningPurchase>
{
    public LearningPurchaseIndexProvider()
    {
        CollectionName = LearningCommerceConstants.CollectionName;
    }

    public override void Describe(DescribeContext<LearningPurchase> context)
        => context.For<LearningPurchaseIndex>().Map(purchase => new LearningPurchaseIndex { ItemId = purchase.ItemId, OrderId = purchase.OrderId, LineId = purchase.LineId, UserId = purchase.UserId });
}

public sealed class LicensePoolIndexProvider : IndexProvider<LicensePool>
{
    public LicensePoolIndexProvider()
    {
        CollectionName = LearningCommerceConstants.CollectionName;
    }

    public override void Describe(DescribeContext<LicensePool> context)
        => context.For<LicensePoolIndex>().Map(pool => new LicensePoolIndex { ItemId = pool.ItemId, AccountId = pool.AccountId, CourseId = pool.CourseId, OrderId = pool.OrderId, LineId = pool.LineId, State = pool.State });
}

public sealed class LicenseSeatIndexProvider : IndexProvider<LicenseSeat>
{
    public LicenseSeatIndexProvider()
    {
        CollectionName = LearningCommerceConstants.CollectionName;
    }

    public override void Describe(DescribeContext<LicenseSeat> context)
        => context.For<LicenseSeatIndex>().Map(seat => new LicenseSeatIndex
        {
            ItemId = seat.ItemId,
            PoolId = seat.PoolId,
            AccountId = seat.AccountId,
            UserId = seat.UserId,
            NormalizedEmail = seat.Email?.Trim().ToUpperInvariant(),
            InvitationId = seat.InvitationId,
            State = seat.State,
        });
}

/// <summary>
/// The index providers of Learning commerce.
/// </summary>
public static class LearningCommerceIndexProviders
{
    public static IIndexProvider[] All() => [new LearningPurchaseIndexProvider(), new LicensePoolIndexProvider(), new LicenseSeatIndexProvider(), new QuantityLedgerEntryIndexProvider(LearningCommerceConstants.SeatLedgerCollectionName), new LeasedWorkItemIndexProvider<LearningFulfillmentWork>(LearningCommerceConstants.FulfillmentWorkCollectionName)];
}

/// <summary>
/// Creates the tables of Learning commerce.
/// </summary>
public static class LearningCommerceIndexSchema
{
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = LearningCommerceConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<LearningPurchaseIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<string>("LineId", column => column.WithLength(50))
            .Column<string>("UserId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<LearningPurchaseIndex>(table =>
        {
            table.CreateIndex("IDX_LearningPurchase_ItemId", "ItemId");
            table.CreateIndex("IDX_LearningPurchase_OrderLine", "OrderId", "LineId");
            table.CreateIndex("IDX_LearningPurchase_User", "UserId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<LicensePoolIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("AccountId", column => column.WithLength(26))
            .Column<string>("CourseId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.Nullable().WithLength(26))
            .Column<string>("LineId", column => column.Nullable().WithLength(50))
            .Column<int>("State"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<LicensePoolIndex>(table =>
        {
            table.CreateIndex("IDX_LicensePool_ItemId", "ItemId");
            table.CreateIndex("IDX_LicensePool_Account", "AccountId", "State");
            table.CreateIndex("IDX_LicensePool_OrderLine", "OrderId", "LineId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<LicenseSeatIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("PoolId", column => column.WithLength(26))
            .Column<string>("AccountId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<string>("NormalizedEmail", column => column.Nullable().WithLength(255))
            .Column<string>("InvitationId", column => column.Nullable().WithLength(26))
            .Column<int>("State"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<LicenseSeatIndex>(table =>
        {
            table.CreateIndex("IDX_LicenseSeat_ItemId", "ItemId");
            table.CreateIndex("IDX_LicenseSeat_Pool", "PoolId", "State");
            table.CreateIndex("IDX_LicenseSeat_User", "UserId");
            table.CreateIndex("IDX_LicenseSeat_Invitation", "InvitationId");
        }, collection: collection);

        await QuantityLedgerIndexSchema.CreateAsync(schemaBuilder, LearningCommerceConstants.SeatLedgerCollectionName);
        await CreateFulfillmentQueueAsync(schemaBuilder);
    }

    /// <summary>
    /// Creates the table of the work queue that fulfils paid purchases and takes refunded ones back.
    /// </summary>
    public static Task CreateFulfillmentQueueAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        return LeasedWorkItemIndexSchema.CreateAsync(schemaBuilder, LearningCommerceConstants.FulfillmentWorkCollectionName);
    }
}
