using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Marketplace;
using CrestApps.OrchardCore.Marketplace.Models;

namespace CrestApps.OrchardCore.Tests.Marketplace;

/// <summary>
/// Review 10 (A2): a payout whose transfer Stripe answered with a server error, and that a later lookup didn't find, was
/// taken as refused: its disbursement was failed (or returned) and the next run sent its earnings under a new key. Stripe
/// can still finish the first request during incident recovery, so the vendor was paid twice. Not found now proves
/// nothing: the payout stays pending under its key until Stripe shows the transfer, or staff confirm none was made.
/// </summary>
public sealed partial class MarketplaceServiceTests
{
    /// <summary>
    /// The transfer is still being made when the next run looks it up, and appears at Stripe only after that lookup.
    /// </summary>
    [Fact]
    public async Task ASubmittedPayout_WhoseTransferAppearsAfterTheFirstLookup_IsPaidOnce_UnderItsOwnKey()
    {
        // Arrange
        var stripe = new FakeStripe { DelayNextTransfer = true };
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false, PayoutProvider = MarketplaceConstants.StripeConnectPayoutProvider }, CreateStripeProvider(stripe));

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.Equal(DisbursementStatus.Submitted, Assert.Single(_disbursements.Store.Entries).Status);

        // Act: the next run finds nothing, then Stripe finishes the transfer, then the run after.
        var (secondPaid, secondFailures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.Equal(0m, secondPaid);
        Assert.Single(secondFailures);
        Assert.Empty(stripe.Transfers);
        Assert.Equal(VendorPayoutStatus.Pending, Assert.Single(_payouts).Status);
        Assert.NotNull(_payouts[0].UnconfirmedSinceUtc);
        Assert.Equal(DisbursementStatus.Submitted, Assert.Single(_disbursements.Store.Entries).Status);

        stripe.CompleteDelayedTransfer();

        var (thirdPaid, thirdFailures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Assert
        var transfer = Assert.Single(stripe.Transfers);
        var payout = Assert.Single(_payouts);

        Assert.Empty(thirdFailures);
        Assert.Equal(48.6m, thirdPaid);
        Assert.Equal(VendorPayoutStatus.Paid, payout.Status);
        Assert.Equal(transfer.Id, payout.TransferId);
        Assert.Equal(DisbursementStatus.Settled, Assert.Single(_disbursements.Store.Entries).Status);
        Assert.All(_earnings, earning => Assert.Equal(VendorEarningStatus.PaidOut, earning.Status));
    }

    /// <summary>
    /// As above, but staff settled the disbursement (K8's path) before the transfer appeared.
    /// </summary>
    [Fact]
    public async Task APayoutSettledWithoutATraceNumber_WhoseTransferAppearsAfterTheFirstLookup_IsPaidOnce_UnderItsOwnKey()
    {
        // Arrange
        var stripe = new FakeStripe { DelayNextTransfer = true };
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false, PayoutProvider = MarketplaceConstants.StripeConnectPayoutProvider }, CreateStripeProvider(stripe));

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.Equal(1, await SettleAsStaffAsync());

        // Act
        var (secondPaid, secondFailures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.Equal(0m, secondPaid);
        Assert.Single(secondFailures);
        Assert.Equal(VendorPayoutStatus.Pending, Assert.Single(_payouts).Status);
        Assert.Equal(DisbursementStatus.Settled, Assert.Single(_disbursements.Store.Entries).Status);

        stripe.CompleteDelayedTransfer();

        var (thirdPaid, thirdFailures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Assert
        var transfer = Assert.Single(stripe.Transfers);
        var payout = Assert.Single(_payouts);
        var disbursement = Assert.Single(_disbursements.Store.Entries);

        Assert.Empty(thirdFailures);
        Assert.Equal(48.6m, thirdPaid);
        Assert.Equal(VendorPayoutStatus.Paid, payout.Status);
        Assert.Equal(transfer.Id, payout.TransferId);
        Assert.Equal(transfer.Id, disbursement.TraceNumber);
    }
}
