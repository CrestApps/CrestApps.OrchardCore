using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.WorkOrders;

namespace CrestApps.OrchardCore.Tests.Records;

/// <summary>
/// A record kind is stored in every record reference, so the kinds the modules name never change.
/// </summary>
public sealed class RecordKindsTests
{
    [Fact]
    public void TheRecordKinds_KeepTheValuesThatRecordReferencesStore()
    {
        Assert.Equal("Order", OrderConstants.RecordKind);
        Assert.Equal("Subscription", SubscriptionConstants.RecordKind);
        Assert.Equal("Transaction", TransactionsConstants.RecordKind);
        Assert.Equal("Asset", AssetsConstants.RecordKind);
        Assert.Equal("WorkOrder", WorkOrdersConstants.RecordKind);
    }
}
