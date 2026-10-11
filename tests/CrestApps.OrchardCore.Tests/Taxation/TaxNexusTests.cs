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
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
        });
    }

    [Fact]
    public async Task NoRegistrations_TaxIsCollected()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedRuleAsync(harness, jurisdictionId);

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

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
        }, TestContext.Current.CancellationToken);

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

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
        }, TestContext.Current.CancellationToken);

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

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
        }, TestContext.Current.CancellationToken);

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task EconomicThresholdNotReached_DoesNotEstablishNexus()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedRuleAsync(harness, jurisdictionId);

        await harness.Registrations.CreateAsync(new MerchantTaxRegistration
        {
            Name = "CA economic nexus",
            JurisdictionId = jurisdictionId,
            TaxType = TaxTypeNames.SalesTax,
            IsActive = true,
            ThresholdAmount = 100_000m,
            ThresholdAccumulatedAmount = 40_000m,
        }, TestContext.Current.CancellationToken);

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task EconomicThresholdReached_EstablishesNexus()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await SeedRuleAsync(harness, jurisdictionId);

        await harness.Registrations.CreateAsync(new MerchantTaxRegistration
        {
            Name = "CA economic nexus",
            JurisdictionId = jurisdictionId,
            TaxType = TaxTypeNames.SalesTax,
            IsActive = true,
            ThresholdAmount = 100_000m,
            ThresholdAccumulatedAmount = 150_000m,
        }, TestContext.Current.CancellationToken);

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

        Assert.Single(result.Lines);
    }
}
