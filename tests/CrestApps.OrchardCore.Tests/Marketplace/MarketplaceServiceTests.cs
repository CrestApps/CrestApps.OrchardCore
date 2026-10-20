using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Marketplace;

/// <summary>
/// A vendor's sale is recorded once, less the commission, held until released; a refund takes back its share; and a
/// payout sends what's released, order by order.
/// </summary>
public sealed class MarketplaceServiceTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<VendorEarning> _earnings = [];
    private readonly List<VendorPayout> _payouts = [];
    private readonly Vendor _vendor = new() { ItemId = "v1", Name = "Northwind Pottery", PayoutsEnabled = true, StripeAccountId = "acct_1" };
    private readonly FakePayoutProvider _provider = new();
    private Order _order;

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
        Assert.Equal(VendorPayoutStatus.Paid, Assert.Single(_payouts).Status);
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

    private MarketplaceService Create(MarketplaceSettings settings)
    {
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
            .Callback((VendorPayout payout, CancellationToken _) => _payouts.Add(payout))
            .Returns(ValueTask.CompletedTask);

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

        return new MarketplaceService(vendors.Object, earnings.Object, payouts.Object, content.Object, orders.Object, site.Object, [_provider], clock.Object, NullLogger<MarketplaceService>.Instance);
    }

    private sealed class FakePayoutProvider : IVendorPayoutProvider
    {
        public decimal Amount { get; private set; }

        public string Currency { get; private set; }

        public Task<(string TransferId, string Failure)> PayAsync(Vendor vendor, Order order, decimal amount, string currency, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            Amount = amount;
            Currency = currency;

            return Task.FromResult<(string, string)>(("tr_1", null));
        }
    }
}
