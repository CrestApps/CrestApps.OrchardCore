using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Marketplace;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Tests.Disbursements;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Marketplace;

/// <summary>
/// A vendor's sale is recorded once, less the commission, held until released; a refund takes back its share; and a
/// payout sends what's released, order by order, as a disbursement through the Disbursements building block.
/// </summary>
public sealed class MarketplaceServiceTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<VendorEarning> _earnings = [];
    private readonly List<VendorPayout> _payouts = [];
    private readonly Vendor _vendor = new() { ItemId = "v1", Name = "Northwind Pottery", PayoutsEnabled = true, StripeAccountId = "acct_1" };
    private readonly FakePayoutProvider _provider = new("Test");
    private DisbursementsTestContext _disbursements;
    private Order _order;
    private bool _failNextPayoutSave;

    [Fact]
    public async Task ASale_IsRecordedOnce_LessTheCommission_AndHeld()
    {
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 7 });

        Assert.Equal(1, await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken));
        Assert.Equal(0, await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken));

        var sale = Assert.Single(_earnings);

        // The 60 mug pays 54 after its share of the 10 discount; the store keeps 5.40.
        Assert.Equal((54m, 5.4m, 48.6m), (sale.Gross, sale.Commission, sale.Net));
        Assert.Equal(VendorEarningStatus.Held, sale.Status);
        Assert.Equal(_now.AddDays(7), sale.HoldUntilUtc);
        Assert.True(sale.AwaitsFulfillment);
    }

    [Fact]
    public async Task ARefund_TakesBackItsShare_AndAPayout_SendsWhatsLeft()
    {
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0 });

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);

        // Half of the 90 paid comes back: half of the vendor's 54 sale.
        await service.RecordRefundAsync(_order, new OrderRefund { RefundId = "r1", Amount = 45m }, TestContext.Current.CancellationToken);
        await service.RecordRefundAsync(_order, new OrderRefund { RefundId = "r1", Amount = 45m }, TestContext.Current.CancellationToken);

        Assert.Equal(2, _earnings.Count);
        Assert.Equal(-24.3m, _earnings[1].Net);

        // Not fulfilled yet: nothing is released.
        Assert.Equal(0, await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken));

        _order.FulfillmentStatus = OrderFulfillmentStatus.Fulfilled;

        Assert.Equal(2, await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken));

        var (paid, failures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.Empty(failures);
        Assert.Equal(24.3m, paid);
        Assert.Equal((24.3m, "EUR"), (_provider.Amount, _provider.Currency));
        Assert.All(_earnings, earning => Assert.Equal(VendorEarningStatus.PaidOut, earning.Status));

        // The payout is a settled disbursement to the vendor for the order, and the transfer is its trace.
        var payout = Assert.Single(_payouts);
        var disbursement = Assert.Single(_disbursements.Store.Entries);
        Assert.Equal(VendorPayoutStatus.Paid, payout.Status);
        Assert.Equal(disbursement.ItemId, payout.DisbursementId);
        Assert.Equal("tr_1", payout.TransferId);
        Assert.Equal(DisbursementStatus.Settled, disbursement.Status);
        Assert.Equal((MarketplaceConstants.VendorPartyKind, "v1"), (disbursement.Payee.Kind, disbursement.Payee.Id));
        Assert.Equal((MarketplaceConstants.PayoutSourceKind, "o1", "v1"), (disbursement.SourceKind, disbursement.SourceId, disbursement.AccessScope));
        Assert.Equal(24.3m, disbursement.Amount);
    }

    [Fact]
    public async Task APayoutRetriedAfterItsSaveFailed_ReusesTheSameIdempotencyKey()
    {
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false });

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        // The transfer goes through, then saving the payout fails, so the earnings are still ready to pay out.
        _failNextPayoutSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PayOutAsync(_vendor, TestContext.Current.CancellationToken));

        var firstKey = _provider.IdempotencyKey;

        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // The same key gives the same settled disbursement, so the vendor is never sent the money twice.
        Assert.False(string.IsNullOrEmpty(firstKey));
        Assert.Equal(1, _provider.Calls);
        Assert.Single(_disbursements.Store.Entries);
        Assert.Equal(VendorPayoutStatus.Paid, Assert.Single(_payouts).Status);
        Assert.Equal("tr_1", _payouts[0].TransferId);
    }

    [Fact]
    public async Task APayoutRetriedAfterTheProviderRefusedIt_UsesANewIdempotencyKey()
    {
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false });

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        _provider.Failure = "Insufficient funds.";

        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        var refusedKey = _provider.IdempotencyKey;

        _provider.Failure = null;

        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // The provider keeps a refused request's answer for its key, so the next try must not reuse it.
        Assert.NotEqual(refusedKey, _provider.IdempotencyKey);
        Assert.Equal([VendorPayoutStatus.Failed, VendorPayoutStatus.Paid], _payouts.Select(payout => payout.Status));
        Assert.Equal("Insufficient funds.", _payouts[0].Failure);
        Assert.Equal([DisbursementStatus.Failed, DisbursementStatus.Settled], _disbursements.Store.Entries.Select(entry => entry.Status));
    }

    [Fact]
    public async Task APayout_UsesTheProviderNamedInTheSettings()
    {
        var other = new FakePayoutProvider("Other");
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false, PayoutProvider = "Other" }, other);

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        var (paid, failures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.Empty(failures);
        Assert.Equal(48.6m, paid);
        Assert.Equal(48.6m, other.Amount);
        Assert.Null(_provider.Currency);
    }

    [Fact]
    public async Task APayout_WithSeveralProvidersAndNoneNamed_IsntSent()
    {
        var other = new FakePayoutProvider("Other");
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false }, other);

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        var (paid, failures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.Equal(0m, paid);
        Assert.Single(failures);
        Assert.Null(_provider.Currency);
        Assert.Null(other.Currency);
    }

    [Fact]
    public async Task APayout_WithoutDisbursements_IsntSent_AndSaysWhy()
    {
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false }, withDisbursements: false);

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        var (paid, failures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.Equal(0m, paid);
        Assert.Contains("Disbursements", Assert.Single(failures), StringComparison.Ordinal);
        Assert.False(service.CanPayOut);
        Assert.Null(_provider.Currency);
    }

    [Fact]
    public async Task AVendorWithoutAPayoutAccount_IsntPaidOut()
    {
        var service = Create(new MarketplaceSettings());

        _vendor.PayoutsEnabled = false;

        var (paid, failures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.Equal(0m, paid);
        Assert.Single(failures);
        Assert.Null(_provider.Currency);
    }

    private MarketplaceService Create(MarketplaceSettings settings, params IVendorPayoutProvider[] otherProviders)
        => Create(settings, true, otherProviders);

    private MarketplaceService Create(MarketplaceSettings settings, bool withDisbursements, params IVendorPayoutProvider[] otherProviders)
    {
        _disbursements = new DisbursementsTestContext([_provider, .. otherProviders]);

        var mug = new ContentItem { ContentItemId = "mug" };

        mug.Weld(new VendorPart { VendorId = _vendor.ItemId });

        _order = new Order
        {
            ItemId = "o1",
            OrderNumber = "261020-TEST",
            Currency = "EUR",
            Subtotal = 100m,
            Total = 90m,
            PaidUtc = _now,
            FulfillmentStatus = OrderFulfillmentStatus.Unfulfilled,
            Discounts = [new OrderDiscount { Amount = 10m }],
            Lines =
            [
                new OrderLine { LineId = "l1", ContentItemId = "mug", Title = "Mug", Quantity = 1, UnitPrice = 60m, LineTotal = 60m, ProductType = ProductType.Good },
                new OrderLine { LineId = "l2", ContentItemId = "own", Title = "Store's own", Quantity = 1, UnitPrice = 40m, LineTotal = 40m },
            ],
        };

        var vendors = new Mock<IVendorStore>();
        vendors.Setup(store => store.FindByIdAsync("v1", It.IsAny<CancellationToken>())).ReturnsAsync(_vendor);

        var earnings = new Mock<IVendorEarningStore>();
        earnings.Setup(store => store.ListByOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string orderId, CancellationToken _) => [.. _earnings.Where(earning => earning.OrderId == orderId)]);
        earnings.Setup(store => store.ListByStatusAsync(It.IsAny<VendorEarningStatus>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorEarningStatus status, DateTime? before, int _, CancellationToken _) => [.. _earnings.Where(earning => earning.Status == status && (before is null || earning.HoldUntilUtc <= before))]);
        earnings.Setup(store => store.ListAvailableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string vendorId, CancellationToken _) => [.. _earnings.Where(earning => earning.VendorId == vendorId && earning.Status == VendorEarningStatus.Available)]);
        earnings.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => _earnings.FirstOrDefault(earning => earning.ItemId == id));
        earnings.Setup(store => store.CreateAsync(It.IsAny<VendorEarning>(), It.IsAny<CancellationToken>()))
            .Callback((VendorEarning earning, CancellationToken _) => _earnings.Add(earning))
            .Returns(ValueTask.CompletedTask);
        earnings.Setup(store => store.UpdateAsync(It.IsAny<VendorEarning>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var payouts = new Mock<IVendorPayoutStore>();
        payouts.Setup(store => store.CreateAsync(It.IsAny<VendorPayout>(), It.IsAny<CancellationToken>()))
            .Returns((VendorPayout payout, CancellationToken _) =>
            {
                if (_failNextPayoutSave)
                {
                    _failNextPayoutSave = false;

                    throw new InvalidOperationException("The save failed.");
                }

                _payouts.Add(payout);

                return ValueTask.CompletedTask;
            });

        var content = new Mock<IContentManager>();
        content.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string id, VersionOptions _) => id == "mug" ? mug : new ContentItem { ContentItemId = id });

        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _order);

        var siteSettings = new Mock<ISite>();
        siteSettings.Setup(value => value.GetOrCreate<MarketplaceSettings>()).Returns(settings);

        var site = new Mock<ISiteService>();
        site.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(siteSettings.Object);

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);

        return new MarketplaceService(
            vendors.Object,
            earnings.Object,
            payouts.Object,
            content.Object,
            orders.Object,
            site.Object,
            [_provider, .. otherProviders],
            withDisbursements ? _disbursements.Services : new ServiceCollection().BuildServiceProvider(),
            new FixedOffsetLocalClock(() => _now, TimeSpan.Zero),
            clock.Object,
            NullLogger<MarketplaceService>.Instance,
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<MarketplaceService>());
    }

    private sealed class FakePayoutProvider : IVendorPayoutProvider
    {
        public FakePayoutProvider(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public LocalizedString DisplayName
            => new(Name, Name);

        public string Method
            => "Transfer" + Name;

        public decimal Amount { get; private set; }

        public string Currency { get; private set; }

        public string IdempotencyKey { get; private set; }

        public int Calls { get; private set; }

        public string Failure { get; set; }

        public Task<DisbursementSubmitResult> SubmitAsync(DisbursementBatch batch, IReadOnlyList<Disbursement> disbursements, CancellationToken cancellationToken = default)
        {
            var result = new DisbursementSubmitResult { Succeeded = true };

            foreach (var disbursement in disbursements)
            {
                Calls++;
                Amount = disbursement.Amount;
                Currency = disbursement.Currency;
                IdempotencyKey = disbursement.IdempotencyKey;

                if (Failure is null)
                {
                    result.Statuses[disbursement.ItemId] = DisbursementStatus.Settled;
                    result.TraceNumbers[disbursement.ItemId] = "tr_1";
                }
                else
                {
                    result.Statuses[disbursement.ItemId] = DisbursementStatus.Failed;
                    result.FailureReasons[disbursement.ItemId] = Failure;
                }
            }

            return Task.FromResult(result);
        }
    }
}
