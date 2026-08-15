using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxServiceTests
{
    private static TaxTestHarness CreateHarness()
        => new(new TestClock(TaxTestData.TransactionDate));

    [Fact]
    public async Task Calculate_WithNoRules_ReturnsZeroTax()
    {
        var harness = CreateHarness();

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.TaxAmount);
        Assert.Equal(100m, result.TotalAmount);
    }

    [Fact]
    public async Task Calculate_ExclusivePercentage_AddsTaxToTotal()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "CA Sales Tax",
            TaxType = TaxTypeNames.SalesTax,
            TaxName = "CA Sales Tax",
            TaxCode = "US-CA-SALES",
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.075m,
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(200m), TestContext.Current.CancellationToken);

        var line = Assert.Single(result.Lines);
        Assert.Equal(15m, line.TaxAmount);
        Assert.Equal(jurisdictionId, line.JurisdictionId);
        Assert.Equal("California", line.JurisdictionName);
        Assert.Equal(200m, result.TaxableAmount);
        Assert.Equal(15m, result.TaxAmount);
        Assert.Equal(215m, result.TotalAmount);
    }

    [Fact]
    public async Task Calculate_InclusivePricing_ExtractsTaxFromPrice()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "VAT",
            TaxType = TaxTypeNames.Vat,
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.20m,
        });

        var context = TaxTestData.Context(120m);
        context.DefaultPriceType = TaxPriceType.Inclusive;

        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        var line = Assert.Single(result.Lines);
        Assert.Equal(20m, line.TaxAmount);
        Assert.True(line.IncludedInPrice);
        Assert.Equal(100m, result.TaxableAmount);
        Assert.Equal(120m, result.TotalAmount);
    }

    [Fact]
    public async Task Calculate_MultipleSimultaneousTaxes_AccumulatesLines()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "State",
            TaxType = TaxTypeNames.SalesTax,
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.06m,
        });

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "City",
            TaxType = TaxTypeNames.SalesTax,
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.025m,
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(8.5m, result.TaxAmount);
    }

    [Fact]
    public async Task Calculate_CompoundTax_TaxesPriorTax()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "Quebec", "CA", "QC");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "GST",
            TaxType = TaxTypeNames.Gst,
            JurisdictionId = jurisdictionId,
            Priority = 1,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.05m,
        });

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "QST",
            TaxType = TaxTypeNames.Qst,
            JurisdictionId = jurisdictionId,
            Priority = 2,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.09975m,
            IsCompound = true,
        });

        var context = TaxTestData.Context(100m, destination: new TaxAddress { Country = "CA", Region = "QC" });
        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        var gst = result.Lines.Single(line => line.TaxType == TaxTypeNames.Gst);
        var qst = result.Lines.Single(line => line.TaxType == TaxTypeNames.Qst);

        Assert.Equal(5m, gst.TaxAmount);
        // QST is compound: (100 + 5) * 0.09975 = 10.47375, rounded per line to 10.47.
        Assert.Equal(10.47m, qst.TaxAmount);
    }

    [Fact]
    public async Task Calculate_ExpiredRule_IsNotApplied()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Old",
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
            EffectiveToUtc = TaxTestData.TransactionDate.AddDays(-1),
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task Calculate_FutureRule_IsNotApplied()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Future",
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
            EffectiveFromUtc = TaxTestData.TransactionDate.AddDays(1),
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task Calculate_CategoryMismatch_SkipsRule()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Alcohol only",
            JurisdictionId = jurisdictionId,
            CategoryCode = "Alcohol",
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
        });

        var electronics = TaxTestData.Context(100m, categoryCode: "Electronics");
        var alcohol = TaxTestData.Context(100m, categoryCode: "Alcohol");

        Assert.Empty((await harness.TaxService.CalculateAsync(electronics, TestContext.Current.CancellationToken)).Lines);
        Assert.Single((await harness.TaxService.CalculateAsync(alcohol, TestContext.Current.CancellationToken)).Lines);
    }

    [Fact]
    public async Task Calculate_B2BRule_DoesNotApplyToB2CCustomer()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "B2B tax",
            JurisdictionId = jurisdictionId,
            CustomerType = CustomerTaxType.B2B,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
        });

        var b2c = TaxTestData.Context(100m, customer: new CustomerTaxProfile { CustomerType = CustomerTaxType.B2C });
        var b2b = TaxTestData.Context(100m, customer: new CustomerTaxProfile { CustomerType = CustomerTaxType.B2B });

        Assert.Empty((await harness.TaxService.CalculateAsync(b2c, TestContext.Current.CancellationToken)).Lines);
        Assert.Single((await harness.TaxService.CalculateAsync(b2b, TestContext.Current.CancellationToken)).Lines);
    }

    [Fact]
    public async Task Calculate_MultiJurisdiction_OnlyMatchingApplies()
    {
        var harness = CreateHarness();
        var californiaId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        var texasId = await TaxTestData.AddJurisdictionAsync(harness, "Texas", "US", "TX");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "CA",
            JurisdictionId = californiaId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.075m,
        });

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "TX",
            JurisdictionId = texasId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.0625m,
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

        var line = Assert.Single(result.Lines);
        Assert.Equal(californiaId, line.JurisdictionId);
        Assert.Equal(7.5m, line.TaxAmount);
    }

    [Fact]
    public async Task Calculate_FixedAmountRule_AppliesFlatTax()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Env fee",
            TaxType = TaxTypeNames.ExciseTax,
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.FixedAmount,
            FixedAmount = 3m,
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

        Assert.Equal(3m, result.TaxAmount);
        Assert.Equal(103m, result.TotalAmount);
    }

    [Fact]
    public async Task Calculate_DiscountReducesTaxableBase()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Sales",
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
        });

        var context = TaxTestData.Context(100m);
        ((TaxableItem)context.Items[0]).DiscountAmount = 20m;

        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        // (100 - 20) * 0.10 = 8
        Assert.Equal(8m, result.TaxAmount);
    }
}
