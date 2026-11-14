using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
public sealed partial class MarketplaceServiceTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<VendorEarning> _earnings = [];
    private readonly List<VendorPayout> _payouts = [];
    private readonly Vendor _vendor = new() { ItemId = "v1", Name = "Northwind Pottery", PayoutsEnabled = true, StripeAccountId = "acct_1" };
    private readonly FakePayoutProvider _provider = new("Test");
    private Order _order;
    private bool _failNextPayoutSave;
    private bool _failNextPayoutUpdate;

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
    public async Task APayoutRetriedAfterItsSaveFailed_ReusesTheSameIdempotencyKey()
    {
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false });

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        // The transfer goes through, then recording that it was paid fails, so the earnings are still ready to pay out.
        _failNextPayoutUpdate = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PayOutAsync(_vendor, TestContext.Current.CancellationToken));

        var firstKey = _provider.IdempotencyKey;

        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(firstKey));
        Assert.Equal(firstKey, _provider.IdempotencyKey);
        Assert.Equal(VendorPayoutStatus.Paid, Assert.Single(_payouts).Status);
    }

    [Fact]
    public async Task APayout_IsRecordedPendingWithItsKey_BeforeTheTransfer()
    {
        // Arrange
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false });

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        VendorPayout recordedBeforeTransfer = null;

        _provider.OnPay = key => recordedBeforeTransfer = _payouts.SingleOrDefault(payout => payout.IdempotencyKey == key);

        // Act
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(recordedBeforeTransfer);
        Assert.True(_commits > 0);
        Assert.Equal(VendorPayoutStatus.Paid, Assert.Single(_payouts).Status);
    }

    [Fact]
    public async Task ARefundAfterThePayout_IsTakenOffTheNextPayout()
    {
        // Arrange
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false });

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Half of the order comes back after the vendor was paid its 48.60: 24.30 is owed back.
        await service.RecordRefundAsync(_order, new OrderRefund { RefundId = "r1", Amount = 45m }, TestContext.Current.CancellationToken);

        // Then the vendor sells the same again on another order.
        var firstOrder = _order;

        _order = new Order
        {
            ItemId = "o2",
            OrderNumber = "261021-TEST",
            Currency = "EUR",
            Subtotal = firstOrder.Subtotal,
            Total = firstOrder.Total,
            PaidUtc = _now,
            Discounts = firstOrder.Discounts,
            Lines = firstOrder.Lines,
        };

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        // Act
        var (paid, failures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(failures);
        Assert.Equal(24.3m, paid);
        Assert.Equal(24.3m, _provider.Amount);
        Assert.All(_earnings, earning => Assert.Equal(VendorEarningStatus.PaidOut, earning.Status));
    }

    /// <summary>
    /// SEC-C11: a debt was taken off a later payout only when that payout covered it whole, so a refund larger than any
    /// single later order was never recovered and the vendor kept both. Later payouts are now held back until it is.
    /// </summary>
    [Fact]
    public async Task ARefundLargerThanTheNextPayout_HoldsPayoutsBackUntilItIsCovered()
    {
        // Arrange: the 48.60 the vendor earned on the first order is paid out, then the whole order is refunded.
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false });

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);
        await service.RecordRefundAsync(_order, new OrderRefund { RefundId = "r1", Amount = 90m }, TestContext.Current.CancellationToken);

        // Act: a smaller order (the vendor nets 27) does not cover the 48.60 owed.
        await SellAsync("o2", lineTotal: 30m);
        var (secondPaid, _) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Then another one does: 27 + 27 - 48.60.
        await SellAsync("o3", lineTotal: 30m);
        var (thirdPaid, failures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0m, secondPaid);
        Assert.Empty(failures);
        Assert.Equal(5.4m, thirdPaid);
        Assert.Equal(5.4m, _provider.Amount);
        Assert.All(_earnings, earning => Assert.Equal(VendorEarningStatus.PaidOut, earning.Status));
    }

    /// <summary>
    /// SEC-C11: a payout whose transfer went through but was never marked paid (the run stopped in between) was matched
    /// again only when the next run planned exactly the same earnings, so a refund arriving meanwhile made it send a new
    /// transfer under a new key. A pending payout is now sent again as it was, with its own key, before anything new.
    /// </summary>
    [Fact]
    public async Task APendingPayout_IsSettledWithItsOwnKey_BeforeARefundThatArrivedMeanwhileIsPlanned()
    {
        // Arrange: the transfer of 48.60 goes through, then recording that it was paid fails.
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false });

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        _failNextPayoutUpdate = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PayOutAsync(_vendor, TestContext.Current.CancellationToken));

        var firstKey = _provider.IdempotencyKey;

        // A refund of the same order arrives before the next run.
        await service.RecordRefundAsync(_order, new OrderRefund { RefundId = "r1", Amount = 45m }, TestContext.Current.CancellationToken);

        // Act
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Assert: the same transfer was asked for again, and nothing new was sent; the refund is owed back.
        Assert.Equal(firstKey, _provider.IdempotencyKey);
        Assert.Equal(48.6m, _provider.Amount);
        Assert.Equal(VendorPayoutStatus.Paid, Assert.Single(_payouts).Status);
        Assert.Equal(VendorEarningStatus.Available, _earnings.Single(earning => earning.Kind == VendorEarningKind.Refund).Status);
    }

    // Records and releases a sale of the vendor's mug on a new order.
    private async Task SellAsync(string orderId, decimal lineTotal)
    {
        var service = _service;

        _order = new Order
        {
            ItemId = orderId,
            OrderNumber = orderId.ToUpperInvariant(),
            Currency = "EUR",
            Subtotal = lineTotal,
            Total = lineTotal,
            PaidUtc = _now,
            Lines = [new OrderLine { LineId = "l1", ContentItemId = "mug", Title = "Mug", Quantity = 1, UnitPrice = lineTotal, LineTotal = lineTotal, ProductType = ProductType.Good }],
        };

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TwoPayoutsOfTheSameVendorAtOnce_PayOnce()
    {
        // Arrange
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false });

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        var transferStarted = new TaskCompletionSource();
        var finishTransfer = new TaskCompletionSource();

        // Only the first transfer waits, so a second one, if any were sent, would not hang the test.
        _provider.Gate = async () =>
        {
            if (_provider.Calls == 1)
            {
                transferStarted.TrySetResult();
                await finishTransfer.Task;
            }
        };

        // Act
        var first = service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        await transferStarted.Task;

        var second = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        finishTransfer.SetResult();
        await first;

        // Assert
        Assert.Equal(0m, second.Paid);
        Assert.Equal(1, _provider.Calls);
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
        payouts.Setup(store => store.UpdateAsync(It.IsAny<VendorPayout>(), It.IsAny<CancellationToken>()))
            .Returns((VendorPayout payout, CancellationToken _) =>
            {
                if (_failNextPayoutUpdate)
                {
                    _failNextPayoutUpdate = false;

                    // The payout stays as it was stored, pending, as a rolled-back save would leave it.
                    payout.Status = VendorPayoutStatus.Pending;
                    payout.TransferId = null;

                    throw new InvalidOperationException("The save failed.");
                }

                return ValueTask.CompletedTask;
            });
        payouts.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => _payouts.FirstOrDefault(payout => payout.ItemId == id));
        payouts.Setup(store => store.ListByVendorAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string vendorId, int _, CancellationToken _) => [.. _payouts.Where(payout => payout.VendorId == vendorId)]);

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

        var session = new Mock<global::YesSql.ISession>();
        session.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                _commits++;

                return Task.CompletedTask;
            });

        var services = new ServiceCollection()
            .AddSingleton(vendors.Object)
            .AddSingleton(earnings.Object)
            .AddSingleton(payouts.Object)
            .AddSingleton(content.Object)
            .AddSingleton(orders.Object)
            .AddSingleton(site.Object)
            .AddSingleton<IEnumerable<IVendorPayoutProvider>>([_provider, .. otherProviders])
            .AddSingleton(clock.Object)
            .AddSingleton(session.Object)
            .AddSingleton<global::OrchardCore.Locking.Distributed.IDistributedLock>(new global::OrchardCore.Locking.LocalLock(NullLogger<global::OrchardCore.Locking.LocalLock>.Instance))
            .AddSingleton<ILogger<MarketplaceService>>(NullLogger<MarketplaceService>.Instance)
            .AddSingleton<IStringLocalizer<MarketplaceService>>(new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<MarketplaceService>())
            .BuildServiceProvider();

        return _service = ActivatorUtilities.CreateInstance<MarketplaceService>(services);
    }

    private int _commits;
    private MarketplaceService _service;

    private sealed class FakePayoutProvider : IVendorPayoutProvider
    {
        public FakePayoutProvider(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public decimal Amount { get; private set; }

        public string Currency { get; private set; }

        public string IdempotencyKey { get; private set; }

        public string Failure { get; set; }

        public int Calls { get; private set; }

        public Action<string> OnPay { get; set; }

        public Func<Task> Gate { get; set; }

        public async Task<VendorPayoutResult> PayAsync(Vendor vendor, Order order, decimal amount, string currency, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            Calls++;
            Amount = amount;
            Currency = currency;
            IdempotencyKey = idempotencyKey;
            OnPay?.Invoke(idempotencyKey);

            if (Gate is not null)
            {
                await Gate();
            }

            if (Failure is not null)
            {
                return VendorPayoutResult.Refused(Failure);
            }

            _transferred.Add(idempotencyKey);

            return VendorPayoutResult.Paid("tr_1");
        }

        // Like a provider's own records: the transfers made, by the key that asked for them.
        public Task<VendorPayoutResult> FindTransferAsync(Vendor vendor, Order order, VendorPayout payout, CancellationToken cancellationToken = default)
            => Task.FromResult(_transferred.Contains(payout.IdempotencyKey) ? VendorPayoutResult.Paid("tr_1") : null);

        private readonly HashSet<string> _transferred = new(StringComparer.Ordinal);
    }
}
