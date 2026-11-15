using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Taxation.Core.Models;
using CrestApps.OrchardCore.Taxation.Core.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Tax, payment and the commerce verticals round money at the currency's own precision from <c>CurrencyScale</c>: no
/// decimals for yen, three for dinars, two for dollars.
/// </summary>
public sealed class CurrencyPrecisionTests
{
    [Theory]
    [InlineData("JPY", 1234.5, 1235)]
    [InlineData("KWD", 1.23456, 1.235)]
    [InlineData("USD", 10.005, 10.01)]
    public void Tax_IsRoundedAtTheCurrencysPrecision_ByDefault(string currency, double value, double expected)
    {
        var strategy = new DefaultTaxRoundingStrategy(Options.Create(new TaxationOptions()));

        Assert.Equal(DecimalConversion.FromDouble(expected), strategy.Round(DecimalConversion.FromDouble(value), currency));
    }

    [Fact]
    public void Tax_KeepsAConfiguredPerCurrencyOverride()
    {
        var options = new TaxationOptions();
        options.CurrencyDecimalPlaces["JPY"] = 2;

        var strategy = new DefaultTaxRoundingStrategy(Options.Create(options));

        Assert.Equal(1234.57m, strategy.Round(1234.567m, "JPY"));
    }

    [Theory]
    [InlineData("JPY", 1234, 1234)]
    [InlineData("KWD", 1.234, 1230)]
    [InlineData("USD", 12.345, 1235)]
    [InlineData("IQD", 1.234, 1230)]
    [InlineData("LYD", 1.234, 1230)]
    [InlineData("ISK", 5.5, 600)]
    [InlineData("UGX", 1000.4, 100000)]
    [InlineData("MGA", 100.4, 100)]
    public void Stripe_SendsTheCurrencysPrecision_WithItsOwnAmountFormat(string currency, double amount, long expected)
    {
        Assert.Equal(expected, StripeCurrency.ToMinorUnits(DecimalConversion.FromDouble(amount), currency));
    }

    [Theory]
    [InlineData("JPY", 1000, 3, 333)]
    [InlineData("KWD", 1, 3, 0.333)]
    [InlineData("USD", 10, 3, 3.33)]
    public void AReturnedUnitsPrice_IsRoundedAtTheCurrencysPrecision(string currency, double lineTotal, int quantity, double expected)
    {
        var line = new OrderLine { LineTotal = DecimalConversion.FromDouble(lineTotal), Quantity = quantity };

        Assert.Equal(DecimalConversion.FromDouble(expected), OrderReturnPolicy.UnitPriceOf(line, currency));
    }

    [Theory]
    [InlineData("JPY", 10005, 10, 9004)]
    [InlineData("KWD", 10.0005, 10, 9.001)]
    [InlineData("USD", 10.05, 10, 9.04)]
    public void ABookingRefund_IsRoundedAtTheCurrencysPrecision(string currency, double price, double feePercent, double expected)
    {
        Assert.Equal(DecimalConversion.FromDouble(expected), BookingSchedule.GetRefund(DecimalConversion.FromDouble(price), DecimalConversion.FromDouble(price), DecimalConversion.FromDouble(feePercent), currency));
    }

    [Theory]
    [InlineData("JPY", 1001, 10, 100, 901)]
    [InlineData("KWD", 1.235, 10, 0.124, 1.111)]
    [InlineData("USD", 54, 10, 5.4, 48.6)]
    public void AVendorsCommission_IsRoundedAtTheCurrencysPrecision(string currency, double gross, double percent, double commission, double net)
    {
        Assert.Equal((DecimalConversion.FromDouble(commission), DecimalConversion.FromDouble(net)), MarketplaceLedger.Split(DecimalConversion.FromDouble(gross), DecimalConversion.FromDouble(percent), currency));
    }
}
