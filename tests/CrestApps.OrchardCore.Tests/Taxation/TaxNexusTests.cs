using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxNexusTests
{
    private static TaxTestHarness CreateHarness()
        => new(new TestClock(TaxTestData.TransactionDate));

    private static async Task<string> SeedRuleAsync(TaxTestHarness harness, string jurisdictionId)
    {
        return await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Sales",
            TaxType = TaxTypeNames.SalesTax,
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
        });
    }

    [Fact]
    public async Task NoRegistrations_TaxIsCollected()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedRuleAsync(harness, jurisdictionId);

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m));

        Assert.Single(result.Lines);
    }

    [Fact]
    public async Task RegistrationCoversJurisdiction_TaxIsCollected()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedRuleAsync(harness, jurisdictionId);

        await harness.Registrations.CreateAsync(new MerchantTaxRegistration
        {
            Name = "CA registration",
            JurisdictionId = jurisdictionId,
            TaxType = TaxTypeNames.SalesTax,
            IsActive = true,
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m));

        Assert.Single(result.Lines);
    }

    [Fact]
    public async Task RegistrationsExistButNoneCoverJurisdiction_TaxIsNotCollected()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedRuleAsync(harness, jurisdictionId);

        await harness.Registrations.CreateAsync(new MerchantTaxRegistration
        {
            Name = "Texas registration",
            JurisdictionId = "texas-jurisdiction",
            TaxType = TaxTypeNames.SalesTax,
            IsActive = true,
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m));

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task InactiveRegistration_DoesNotEstablishNexus()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedRuleAsync(harness, jurisdictionId);

        await harness.Registrations.CreateAsync(new MerchantTaxRegistration
        {
            Name = "Inactive CA",
            JurisdictionId = jurisdictionId,
            TaxType = TaxTypeNames.SalesTax,
            IsActive = false,
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m));

        Assert.Empty(result.Lines);
    }
}
