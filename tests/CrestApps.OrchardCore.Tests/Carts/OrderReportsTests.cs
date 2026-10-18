using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Reports;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Customers;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// The order reports add up to the orders: what they charged, gave back and kept, by day, product, country and code.
/// </summary>
public sealed class OrderReportsTests
{
    private static readonly DateTime _day = new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOrderStore _orders = new();

    [Fact]
    public async Task TheSalesForADay_WithACouponOrderAndARefund_MatchTheOrders()
    {
        // $129 watch, 10% coupon, $6 shipping, $12.21 tax: $134.31, then $50 given back.
        _orders.Orders.Add(Order("o1", 129m, discount: 12.90m, shipping: 6m, tax: 12.21m, total: 134.31m, paid: _day.AddHours(9), country: "US", refund: 50m));

        // $79 backpack, $7 shipping, $8.60 tax: $94.60.
        _orders.Orders.Add(Order("o2", 79m, discount: 0m, shipping: 7m, tax: 8.60m, total: 94.60m, paid: _day.AddHours(15), country: "CA"));

        // Paid the day before, and an unpaid order: neither counts.
        _orders.Orders.Add(Order("o3", 20m, discount: 0m, shipping: 0m, tax: 0m, total: 20m, paid: _day.AddHours(-3), country: "US"));
        _orders.Orders.Add(new Order { ItemId = "o4", Status = OrderStatus.AwaitingPayment, Currency = "USD", Subtotal = 50m, CreatedUtc = _day.AddHours(10) });

        var document = await Run(new OrderSalesReport(new OrderReportReader(_orders), Site(), Clock(), new PassThroughLocalizer<OrderSalesReport>()));

        var metrics = document.Sections[0].Metrics.ToDictionary(metric => metric.Label, metric => metric.Value);
        Assert.Equal("2", metrics["Orders"]);
        Assert.Equal("$208", metrics["Gross sales"]);
        Assert.Equal("$12.90", metrics["Discounts"]);
        Assert.Equal("$13", metrics["Shipping"]);
        Assert.Equal("$20.81", metrics["Tax"]);
        Assert.Equal("$228.91", metrics["Total charged"]);
        Assert.Equal("$50", metrics["Refunded"]);
        Assert.Equal("$178.91", metrics["Net"]);

        var table = document.Sections.Single(section => section.Rows.Count > 0);
        var day = table.Rows[0].Cells;
        Assert.Equal("2026-10-18", day[0]);
        Assert.Equal("$228.91", day[6]);
    }

    [Fact]
    public void ProductsCountriesAndCodes_AreSummedPerCurrency()
    {
        Order[] orders =
        [
            Order("o1", 129m, discount: 12.90m, shipping: 6m, tax: 12.21m, total: 134.31m, paid: _day, country: "US", code: "SAVE10"),
            Order("o2", 129m, discount: 12.90m, shipping: 6m, tax: 12.21m, total: 134.31m, paid: _day, country: "US", code: "save10"),
            Order("o3", 79m, discount: 0m, shipping: 7m, tax: 0m, total: 86m, paid: _day, country: "CA", currency: "CAD"),
        ];

        var products = OrderReportAggregator.ByProduct(orders);
        Assert.Equal(2, products.Single(product => product.Currency == "USD").Units);
        Assert.Equal(258m, products.Single(product => product.Currency == "USD").Sales);

        var countries = OrderReportAggregator.ByCountry(orders);
        Assert.Equal(268.62m, countries.Single(country => country.Country == "US").Total);
        Assert.Equal("CAD", countries.Single(country => country.Country == "CA").Currency);

        var code = Assert.Single(OrderReportAggregator.ByDiscount(orders));
        Assert.Equal(2, code.Orders);
        Assert.Equal(25.80m, code.Amount);

        // Amounts in two currencies are never added.
        Assert.Equal(["USD", "CAD"], OrderReportAggregator.Summarize(orders).Select(totals => totals.Currency));
    }

    [Fact]
    public async Task TheRefundsReport_ListsTheRefundsGivenInThePeriod_EvenForOlderOrders()
    {
        var older = Order("o1", 100m, discount: 0m, shipping: 0m, tax: 0m, total: 100m, paid: _day.AddDays(-20), country: "US");
        older.Refunds.Add(new OrderRefund { RefundId = "r1", Amount = 40m, Currency = "USD", Reason = "Late delivery", CreatedUtc = _day.AddHours(11) });
        older.Refunds.Add(new OrderRefund { RefundId = "r0", Amount = 10m, Currency = "USD", CreatedUtc = _day.AddDays(-10) });
        _orders.Orders.Add(older);

        var document = await Run(new OrderRefundsReport(new OrderReportReader(_orders), Site(), Clock(), new PassThroughLocalizer<OrderRefundsReport>()));

        var row = Assert.Single(document.Sections.Single(section => section.Rows.Count > 0).Rows);
        Assert.Equal(["2026-10-18", "N-o1", "Late delivery", "$40"], row.Cells);
    }

    [Fact]
    public async Task OrdersToShip_ListsThePaidOrdersNotShippedInFull_OldestFirst()
    {
        var newer = Order("o1", 10m, 0m, 0m, 0m, 10m, _day.AddHours(5), "US");
        newer.FulfillmentStatus = OrderFulfillmentStatus.Unfulfilled;
        var older = Order("o2", 10m, 0m, 0m, 0m, 10m, _day.AddDays(-3), "US");
        older.FulfillmentStatus = OrderFulfillmentStatus.PartiallyFulfilled;
        var shipped = Order("o3", 10m, 0m, 0m, 0m, 10m, _day, "US");
        shipped.FulfillmentStatus = OrderFulfillmentStatus.Fulfilled;

        // Paid with a shipping method and nothing shipped yet; and a download, with nothing to ship.
        var fresh = Order("o4", 10m, 0m, 5m, 0m, 15m, _day.AddHours(1), "US");
        var download = Order("o5", 10m, 0m, 0m, 0m, 10m, _day.AddHours(2), "US");
        _orders.Orders.AddRange([newer, older, shipped, fresh, download]);

        var document = await Run(new UnfulfilledOrdersReport(new OrderReportReader(_orders), Site(), Clock(), new PassThroughLocalizer<UnfulfilledOrdersReport>()));

        Assert.Equal(["N-o2", "N-o4", "N-o1"], document.Sections.Single(section => section.Rows.Count > 0).Rows.Select(row => row.Cells[0]));
    }

    [Fact]
    public async Task StockOnHand_PutsWhatIsOutAndLowFirst()
    {
        var store = new Mock<IStockItemStore>();
        store.Setup(value => value.ListForExportAsync(0, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new StockItem { Title = "Backpack", Sku = "BAG", OnHand = 40, LowStockThreshold = 5 },
            new StockItem { Title = "Mug", Sku = "MUG", OnHand = 3, Reserved = 1, LowStockThreshold = 5 },
            new StockItem { Title = "Watch", Sku = "WAT", OnHand = 0, LowStockThreshold = 5 },
        ]);

        var document = await new StockOnHandReport(store.Object, new PassThroughLocalizer<StockOnHandReport>()).RunAsync(new ReportContext(new ReportFilter()), TestContext.Current.CancellationToken);

        var rows = document.Sections.Single(section => section.Rows.Count > 0).Rows;
        Assert.Equal(["Watch", "Mug", "Backpack"], rows.Select(row => row.Cells[0]));
        Assert.Equal(["Mug", "MUG", "3", "1", "2", "Low"], rows[1].Cells);
    }

    private static async Task<ReportDocument> Run(IReport report)
    {
        var filter = new ReportFilter();
        filter.SetDateRange(new ReportDateRange { FromUtc = _day, ToUtc = _day.AddDays(1) });

        return await report.RunAsync(new ReportContext(filter), TestContext.Current.CancellationToken);
    }

    private static Order Order(string id, decimal lines, decimal discount, decimal shipping, decimal tax, decimal total, DateTime paid, string country, decimal refund = 0m, string code = "SAVE10", string currency = "USD")
    {
        var order = new Order
        {
            ItemId = id,
            OrderNumber = "N-" + id,
            Status = OrderStatus.Paid,
            Currency = currency,
            CreatedUtc = paid.AddMinutes(-5),
            PaidUtc = paid,
            Subtotal = lines,
            TaxAmount = tax,
            Total = total,
            ShippingAddress = new Address { Country = country },
            Shipping = shipping > 0m ? new OrderShipping { Name = "Standard", Amount = shipping } : null,
            Lines = [new OrderLine { LineId = id + "-1", ContentItemId = "product-" + lines, Title = "Item " + lines, Sku = "SKU", Quantity = 1, UnitPrice = lines, LineTotal = lines }],
        };

        if (discount > 0m)
        {
            order.Discounts.Add(new OrderDiscount { Code = code, Amount = discount });
        }

        if (refund > 0m)
        {
            order.Refunds.Add(new OrderRefund { RefundId = "r-" + id, Amount = refund, Currency = currency, CreatedUtc = paid.AddHours(1) });
        }

        return order;
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
        clock.Setup(value => value.UtcNow).Returns(_day.AddHours(20));

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
