using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxExemptionTests
{
    private static TaxTestHarness CreateHarness()
        => new(new TestClock(TaxTestData.TransactionDate));

    private static async Task<string> SeedTaxedRuleAsync(TaxTestHarness harness, string jurisdictionId)
    {
        return await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Sales",
            TaxType = TaxTypeNames.SalesTax,
            JurisdictionId = jurisdictionId,
            CategoryCode = "Electronics",
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
        });
    }

    [Fact]
    public async Task Customer_MarkedTaxExempt_ProducesNoTax()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedTaxedRuleAsync(harness, jurisdictionId);

        var context = TaxTestData.Context(100m, customer: new CustomerTaxProfile { IsTaxExempt = true }, categoryCode: "Electronics");
        var result = await harness.TaxService.CalculateAsync(context);

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task ValidCertificate_ExemptsTax()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedTaxedRuleAsync(harness, jurisdictionId);

        var certificate = new ExemptionCertificate
        {
            Name = "Resale",
            CertificateNumber = "RS-1",
            Status = ExemptionStatus.Active,
        };

        await harness.Exemptions.CreateAsync(certificate);

        var customer = new CustomerTaxProfile { ExemptionCertificateIds = [certificate.ItemId] };
        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m, customer: customer, categoryCode: "Electronics"));

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task ExpiredCertificate_DoesNotExempt()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedTaxedRuleAsync(harness, jurisdictionId);

        var certificate = new ExemptionCertificate
        {
            Name = "Expired",
            CertificateNumber = "EX-1",
            Status = ExemptionStatus.Active,
            ExpirationUtc = TaxTestData.TransactionDate.AddDays(-1),
        };

        await harness.Exemptions.CreateAsync(certificate);

        var customer = new CustomerTaxProfile { ExemptionCertificateIds = [certificate.ItemId] };
        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m, customer: customer, categoryCode: "Electronics"));

        Assert.Single(result.Lines);
    }

    [Fact]
    public async Task JurisdictionSpecificCertificate_OnlyExemptsMatchingJurisdiction()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedTaxedRuleAsync(harness, jurisdictionId);

        var certificate = new ExemptionCertificate
        {
            Name = "Other jurisdiction",
            CertificateNumber = "OJ-1",
            Status = ExemptionStatus.Active,
            JurisdictionIds = ["some-other-jurisdiction"],
        };

        await harness.Exemptions.CreateAsync(certificate);

        var customer = new CustomerTaxProfile { ExemptionCertificateIds = [certificate.ItemId] };
        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m, customer: customer, categoryCode: "Electronics"));

        Assert.Single(result.Lines);
    }

    [Fact]
    public async Task ClassificationSpecificCertificate_OnlyExemptsMatchingClassification()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedTaxedRuleAsync(harness, jurisdictionId);

        var certificate = new ExemptionCertificate
        {
            Name = "Food only",
            CertificateNumber = "FD-1",
            Status = ExemptionStatus.Active,
            ClassificationCodes = ["Food"],
        };

        await harness.Exemptions.CreateAsync(certificate);

        var customer = new CustomerTaxProfile { ExemptionCertificateIds = [certificate.ItemId] };
        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m, customer: customer, categoryCode: "Electronics"));

        Assert.Single(result.Lines);
    }
}
