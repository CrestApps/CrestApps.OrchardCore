using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxTreatmentTests
{
    private static TaxTestHarness CreateHarness()
        => new(new TestClock(TaxTestData.TransactionDate));

    [Fact]
    public async Task ReverseChargeRule_ForB2BCustomer_EmitsZeroRatedLine()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "Germany", "DE", null);

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "VAT",
            TaxType = TaxTypeNames.Vat,
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.19m,
            ReverseCharge = true,
        });

        var context = TaxTestData.Context(100m, customer: new CustomerTaxProfile { CustomerType = CustomerTaxType.B2B });
        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        var line = Assert.Single(result.Lines);

        Assert.Equal(0m, line.TaxAmount);
        Assert.Equal(TaxTreatment.ReverseCharge, line.Treatment);
        Assert.False(string.IsNullOrEmpty(line.TreatmentReason));
        Assert.Equal(0m, result.TaxAmount);
    }

    [Fact]
    public async Task ReverseChargeRule_ForB2CCustomer_ChargesTaxNormally()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "Germany", "DE", null);

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "VAT",
            TaxType = TaxTypeNames.Vat,
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.19m,
            ReverseCharge = true,
        });

        var context = TaxTestData.Context(100m, customer: new CustomerTaxProfile { CustomerType = CustomerTaxType.B2C });
        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        var line = Assert.Single(result.Lines);

        Assert.Equal(19m, line.TaxAmount);
        Assert.Equal(TaxTreatment.Taxable, line.Treatment);
    }

    [Fact]
    public async Task InclusivePricing_WithFixedAmountTax_UnGrossesTheBase()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Recycling fee",
            TaxType = TaxTypeNames.ExciseTax,
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.FixedAmount,
            FixedAmount = 3m,
        });

        var context = TaxTestData.Context(100m);
        context.DefaultPriceType = TaxPriceType.Inclusive;

        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        var line = Assert.Single(result.Lines);

        Assert.Equal(3m, line.TaxAmount);
        Assert.True(line.IncludedInPrice);
        Assert.Equal(97m, result.TaxableAmount);
        Assert.Equal(100m, result.TotalAmount);
    }

    [Fact]
    public async Task InclusivePricing_WithFixedAndPercentageTaxes_UnGrossesBoth()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Fee",
            TaxType = TaxTypeNames.ExciseTax,
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.FixedAmount,
            FixedAmount = 2m,
        });

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "VAT",
            TaxType = TaxTypeNames.Vat,
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.20m,
        });

        var context = TaxTestData.Context(122m);
        context.DefaultPriceType = TaxPriceType.Inclusive;

        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        // net = (122 - 2) / (1 + 0.20) = 100; VAT = 20; fee = 2; gross stays 122.
        Assert.Equal(100m, result.TaxableAmount);
        Assert.Equal(22m, result.TaxAmount);
        Assert.Equal(122m, result.TotalAmount);
    }
}
