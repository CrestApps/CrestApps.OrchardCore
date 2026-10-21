using CrestApps.OrchardCore.Marketplace.Services;

namespace CrestApps.OrchardCore.Tests.Marketplace;

/// <summary>
/// A vendor is owed what its line paid, after its share of the order's discounts, less the store's commission; a
/// refund takes back the same share of it.
/// </summary>
public sealed class MarketplaceLedgerTests
{
    [Fact]
    public void ALine_PaysItsTotalLessItsShareOfTheOrdersDiscount()
    {
        // A 10 discount on a 100 order: the 60 line bears 6 of it.
        Assert.Equal(54m, MarketplaceLedger.GetLineGross(60m, 100m, 10m));
        Assert.Equal(60m, MarketplaceLedger.GetLineGross(60m, 100m, 0m));
        Assert.Equal(0m, MarketplaceLedger.GetLineGross(0m, 100m, 10m));
    }

    [Fact]
    public void TheStore_KeepsItsCommission_AndTheVendorGetsTheRest()
    {
        Assert.Equal((5.4m, 48.6m), MarketplaceLedger.Split(54m, 10m));
        Assert.Equal((0m, 54m), MarketplaceLedger.Split(54m, 0m));
        Assert.Equal((-5.4m, -48.6m), MarketplaceLedger.Split(-54m, 10m));
    }

    [Fact]
    public void ARefund_TakesBackTheSameShareOfEachSale()
    {
        // Half of a 100 order refunded takes back half of a 54 line.
        Assert.Equal(27m, MarketplaceLedger.GetRefundShare(54m, 50m, 100m));
        Assert.Equal(54m, MarketplaceLedger.GetRefundShare(54m, 120m, 100m));
        Assert.Equal(0m, MarketplaceLedger.GetRefundShare(54m, 50m, 0m));
    }
}
