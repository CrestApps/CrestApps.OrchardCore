using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Reports;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Reports;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Reports;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Reports;
using CrestApps.OrchardCore.Tests.Customers;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// The commerce reports beyond sales add up: order value, categories, kinds of product, first-time and returning buyers,
/// checkouts, shipping times, stock movements and download links.
/// </summary>
public sealed class CommerceInsightReportsTests
{
    private static readonly DateTime _day = new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AverageOrderValue_IsWhatWasCharged_OverTheOrders()
    {
        var totals = Assert.Single(OrderReportAggregator.Summarize(
        [
            Order("o1", total: 100m, paid: _day, quantity: 3),
            Order("o2", total: 50m, paid: _day, quantity: 1),
            Order("o3", total: 25.01m, paid: _day, quantity: 1),
        ]));

        Assert.Equal(58.34m, totals.AverageOrderValue);
        Assert.Equal(1.7m, totals.ItemsPerOrder);
    }

    [Fact]
    public void Categories_AreSummedPerTaxonomy_WithLinesInNoneOfItsTerms()
    {
        var taxonomies = new Dictionary<string, string> { ["watches"] = "Category", ["gifts"] = "Category", ["contoso"] = "Brand" };

        var categories = OrderReportAggregator.ByCategory(
        [
            Order("o1", total: 100m, paid: _day, categories: ["watches", "gifts", "contoso"]),
            Order("o2", total: 40m, paid: _day, categories: ["gifts"]),
            Order("o3", total: 10m, paid: _day),
        ], id => taxonomies.GetValueOrDefault(id));

        CategorySales Find(string taxonomy, string id)
            => categories.Single(category => category.Taxonomy == taxonomy && category.CategoryId == id);

        // A line in two categories counts in both.
        Assert.Equal(140m, Find("Category", "gifts").Sales);
        Assert.Equal(2, Find("Category", "gifts").Orders);
        Assert.Equal(100m, Find("Category", "watches").Sales);
        Assert.Equal(10m, Find("Category", string.Empty).Sales);

        // Brands are their own table, and the lines without a brand add up the rest.
        Assert.Equal(100m, Find("Brand", "contoso").Sales);
        Assert.Equal(50m, Find("Brand", string.Empty).Sales);
        Assert.Equal(150m, categories.Where(category => category.Taxonomy == "Brand").Sum(category => category.Sales));
    }

    [Fact]
    public void KindsOfProduct_AreSummedApart()
    {
        var kinds = OrderReportAggregator.ByProductType(
        [
            Order("o1", total: 100m, paid: _day, productType: ProductType.Good),
            Order("o2", total: 12m, paid: _day, productType: ProductType.Digital),
            Order("o3", total: 8m, paid: _day, productType: ProductType.Digital),
        ]);

        Assert.Equal(100m, kinds.Single(kind => kind.ProductType == ProductType.Good).Sales);
        Assert.Equal(20m, kinds.Single(kind => kind.ProductType == ProductType.Digital).Sales);
        Assert.Equal(2, kinds.Single(kind => kind.ProductType == ProductType.Digital).Orders);
    }

    [Fact]
    public async Task Customers_TellFirstTimeBuyersFromReturningOnes_AndSumWhatEachSpentInAll()
    {
        var store = new InMemoryOrderStore();

        // Jane bought last month and again today; Sam bought for the first time today, twice; a guest's email counts
        // as one buyer whatever its case.
        store.Orders.Add(Order("o1", total: 80m, paid: _day.AddDays(-30), email: "jane@example.test", customerId: "jane"));
        store.Orders.Add(Order("o2", total: 20m, paid: _day.AddHours(9), email: "jane@example.test", customerId: "jane", refund: 5m));
        store.Orders.Add(Order("o3", total: 30m, paid: _day.AddHours(10), email: "sam@example.test", customerId: "sam"));
        store.Orders.Add(Order("o4", total: 40m, paid: _day.AddHours(11), email: "sam@example.test", customerId: "sam"));
        store.Orders.Add(Order("o5", total: 10m, paid: _day.AddHours(12), email: "Guest@Example.test"));

        var report = new OrderCustomersReport(new OrderReportReader(store), Site(), Clock(), new PassThroughLocalizer<OrderCustomersReport>());
        var document = await Run(report);
        var metrics = document.Sections[0].Metrics.ToDictionary(metric => metric.Label, metric => metric.Value);

        Assert.Equal("3", metrics["Buyers"]);
        Assert.Equal("2", metrics["First-time buyers"]);
        Assert.Equal("1", metrics["Returning buyers"]);

        // Jane 80 + 20 - 5 refunded = 95, Sam 70, the guest 10: 175 over 3.
        Assert.Equal("$58.33", metrics["Average lifetime spend"]);

        var top = document.Sections.Single(section => section.Rows.Count > 0).Rows;
        Assert.Equal("jane@example.test", top[0].Cells[1]);
        Assert.Equal("$95", top[0].Cells[5]);
        Assert.Equal("2", top[1].Cells[2]);
    }

    [Fact]
    public void Checkouts_AreCountedByHowTheyEnded()
    {
        var funnel = OrderReportAggregator.Funnel(
        [
            CheckoutSessionStatus.Completed,
            CheckoutSessionStatus.Completed,
            CheckoutSessionStatus.Expired,
            CheckoutSessionStatus.Canceled,
            CheckoutSessionStatus.Failed,
            CheckoutSessionStatus.Pending,
            CheckoutSessionStatus.AwaitingProvider,
            CheckoutSessionStatus.PaymentPending,
        ]);

        Assert.Equal(8, funnel.Started);
        Assert.Equal(2, funnel.Paid);
        Assert.Equal(2, funnel.Abandoned);
        Assert.Equal(1, funnel.Failed);
        Assert.Equal(3, funnel.Open);
        Assert.Equal(0.25d, funnel.PaidRate);
    }

    [Fact]
    public void ShippingTimes_RunFromPaymentToLeaving_AndFromLeavingToDelivered()
    {
        Shipment[] shipments =
        [
            new() { OrderId = "o1", ShippedUtc = _day.AddHours(6), DeliveredUtc = _day.AddDays(2).AddHours(6) },
            new() { OrderId = "o2", ShippedUtc = _day.AddDays(3) },
            new() { OrderId = "o3", ShippedUtc = _day.AddDays(1) },
            new() { OrderId = "o4" },
        ];

        var times = ShippingTimes.Measure(shipments, orderId => orderId == "o3" ? null : _day);

        Assert.Equal(3, times.Count);
        Assert.Equal(TimeSpan.FromHours(6), times[0].ToShip);
        Assert.Equal(TimeSpan.FromDays(2), times[0].ToDeliver);
        Assert.Null(times[2].ToShip);

        // 6 hours and 3 days: the median of two is their middle.
        Assert.Equal(TimeSpan.FromHours(39), ShippingTimes.Median(times.Select(time => time.ToShip)));
        Assert.Equal(TimeSpan.FromHours(39), ShippingTimes.Average(times.Select(time => time.ToShip)));
        Assert.Null(ShippingTimes.Average([]));
    }

    [Fact]
    public void StockMovements_AreSummedPerItem_WithDaysLeftAtThePeriodsRateOfSale()
    {
        StockMovement[] movements =
        [
            new() { StockItemId = "cap", Kind = StockMovementKind.Received, Quantity = 20 },
            new() { StockItemId = "cap", Kind = StockMovementKind.Sold, Quantity = -7 },
            new() { StockItemId = "cap", Kind = StockMovementKind.Sold, Quantity = -3 },
            new() { StockItemId = "cap", Kind = StockMovementKind.Returned, Quantity = 1 },
            new() { StockItemId = "cap", Kind = StockMovementKind.Counted, Quantity = -2 },
            new() { StockItemId = "bag", Kind = StockMovementKind.Adjusted, Quantity = 4 },
        ];

        var items = new Dictionary<string, StockItem>
        {
            ["cap"] = new() { ItemId = "cap", Title = "Cap", Sku = "CAP", OnHand = 9 },
            ["bag"] = new() { ItemId = "bag", Title = "Bag", Sku = "BAG", OnHand = 4 },
        };

        var totals = StockMovementTotals.ByItem(movements, id => items.GetValueOrDefault(id), days: 10);

        var cap = totals[0];
        Assert.Equal("Cap", cap.Title);
        Assert.Equal(20, cap.Received);
        Assert.Equal(10, cap.Sold);
        Assert.Equal(1, cap.Returned);
        Assert.Equal(-2, cap.Corrected);

        // 10 sold in 10 days is 1 a day, so 9 on hand last 9 days.
        Assert.Equal(9d, cap.DaysLeft);

        var bag = totals[1];
        Assert.Equal(4, bag.Corrected);
        Assert.Null(bag.DaysLeft);
    }

    [Fact]
    public void DownloadLinks_AreCountedByWhatBecameOfThem()
    {
        var now = _day.AddDays(40);

        DownloadEntitlement[] entitlements =
        [
            Link(downloads: 3, limit: 3),
            Link(downloads: 1, limit: 3),
            Link(downloads: 0, limit: 3, expires: _day.AddDays(30)),
            Link(downloads: 2, limit: 3, revoked: true),
            Link(downloads: 0, limit: 3, expires: now.AddDays(1)),
        ];

        var total = Assert.Single(DownloadTotals.Summarize(entitlements, now));

        Assert.Equal(5, total.Links);
        Assert.Equal(3, total.Used);
        Assert.Equal(2, total.NeverUsed);
        Assert.Equal(6, total.Downloads);
        Assert.Equal(1, total.UsedUp);
        Assert.Equal(1, total.Expired);
        Assert.Equal(1, total.Revoked);
    }

    private static DownloadEntitlement Link(int downloads, int limit, DateTime? expires = null, bool revoked = false)
        => new()
        {
            ProductId = "ebook",
            ProductTitle = "Field guide",
            FileId = "file",
            FileName = "guide.pdf",
            DownloadCount = downloads,
            DownloadLimit = limit,
            ExpiresUtc = expires,
            RevokedUtc = revoked ? _day : null,
            CreatedUtc = _day,
        };

    private static Order Order(
        string id,
        decimal total,
        DateTime paid,
        int quantity = 1,
        string[] categories = null,
        ProductType productType = ProductType.Good,
        string email = null,
        string customerId = null,
        decimal refund = 0m)
    {
        var order = new Order
        {
            ItemId = id,
            OrderNumber = "N-" + id,
            Status = OrderStatus.Paid,
            Currency = "USD",
            CreatedUtc = paid.AddMinutes(-5),
            PaidUtc = paid,
            Subtotal = total,
            Total = total,
            CustomerId = customerId,
            CustomerEmail = email,
            CustomerName = email is null ? null : email.Split('@')[0],
            Lines = [new OrderLine { LineId = id + "-1", ContentItemId = "product-" + id, Title = "Item " + id, Quantity = quantity, UnitPrice = total / quantity, LineTotal = total, ProductType = productType, CategoryIds = categories ?? [] }],
        };

        if (refund > 0m)
        {
            order.Refunds.Add(new OrderRefund { RefundId = "r-" + id, Amount = refund, Currency = "USD", CreatedUtc = paid.AddHours(1) });
        }

        return order;
    }

    private static async Task<ReportDocument> Run(OrderCustomersReport report)
    {
        var filter = new ReportFilter();
        filter.SetDateRange(new ReportDateRange { FromUtc = _day, ToUtc = _day.AddDays(1) });

        return await report.RunAsync(new ReportContext(filter), TestContext.Current.CancellationToken);
    }

    private static ISiteService Site()
    {
        var site = new Mock<ISite>();
        site.SetupGet(value => value.TimeZoneId).Returns("UTC");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_day.AddDays(1));

        return clock.Object;
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
