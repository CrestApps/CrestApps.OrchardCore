using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Reports;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// The one-stop-shop return declares the VAT charged in other EU countries, by country and rate: not the store's own
/// country, not countries outside the EU, and not what was refunded.
/// </summary>
public sealed class VatOssReportTests
{
    [Fact]
    public void VatInOtherEuCountries_IsAddedUpByCountryAndRate()
    {
        Order[] orders =
        [
            Paid("1", "DE", 66.98m, Vat(0.19m, 56.29m, 10.69m)),
            Paid("2", "DE", 119m, Vat(0.19m, 100m, 19m)),
            Paid("3", "FR", 120m, Vat(0.20m, 100m, 20m)),
            Paid("4", "AT", 110m, Vat(0.10m, 100m, 10m), Vat(0.20m, 50m, 10m)),
        ];

        var rows = OrderVatOss.Summarize(orders, originCountry: null);

        Assert.Equal(
            [("AT", 0.20m, 1, 50m, 10m), ("AT", 0.10m, 1, 100m, 10m), ("DE", 0.19m, 2, 156.29m, 29.69m), ("FR", 0.20m, 1, 100m, 20m)],
            rows.Select(row => (row.Country, row.Rate, row.Orders, row.Taxable, row.Vat)));
    }

    [Fact]
    public void TheStoresOwnCountry_AndCountriesOutsideTheEu_AreLeftOut()
    {
        Order[] orders =
        [
            Paid("1", "DE", 119m, Vat(0.19m, 100m, 19m)),
            Paid("2", "GB", 120m, Vat(0.20m, 100m, 20m)),
            Paid("3", "US", 108m, new TaxLine { TaxType = "SalesTax", Rate = 0.08m, TaxableAmount = 100m, TaxAmount = 8m }),
            Paid("4", "FR", 120m, Vat(0.20m, 100m, 20m)),
        ];

        var rows = OrderVatOss.Summarize(orders, originCountry: "de");

        Assert.Equal(["FR"], rows.Select(row => row.Country));
    }

    [Fact]
    public void WhatWasRefunded_IsTakenOffInProportion()
    {
        var order = Paid("1", "FR", 120m, Vat(0.20m, 100m, 20m));
        order.Refunds.Add(new OrderRefund { RefundId = "r1", Amount = 30m, Currency = "EUR" });

        var row = Assert.Single(OrderVatOss.Summarize([order], originCountry: null));

        Assert.Equal(75m, row.Taxable);
        Assert.Equal(15m, row.Vat);
    }

    private static TaxLine Vat(decimal rate, decimal taxable, decimal amount)
        => new() { TaxType = "VAT", TaxName = "VAT", Rate = rate, TaxableAmount = taxable, TaxAmount = amount, IncludedInPrice = true };

    private static Order Paid(string id, string country, decimal total, params TaxLine[] taxes)
        => new()
        {
            ItemId = id,
            Currency = "EUR",
            Status = OrderStatus.Paid,
            Total = total,
            ShippingAddress = new Address { Country = country },
            TaxLines = [.. taxes],
        };
}
