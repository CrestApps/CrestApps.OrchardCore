using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxProviderExtensibilityTests
{
    [Fact]
    public async Task CustomCalculationMethod_CanBeRegisteredAndUsedByRule()
    {
        var harness = new TaxTestHarness(
            new TestClock(TaxTestData.TransactionDate),
            services => services.AddTaxCalculationMethod<FlatTenTaxCalculationMethod>());

        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Flat ten",
            JurisdictionId = jurisdictionId,
            CalculationMethod = FlatTenTaxCalculationMethod.MethodName,
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(250m));

        var line = Assert.Single(result.Lines);
        Assert.Equal(10m, line.TaxAmount);
    }

    [Fact]
    public async Task ExternalDeterminationProvider_ShortCircuitsTheEngine()
    {
        var harness = new TaxTestHarness(
            new TestClock(TaxTestData.TransactionDate),
            services => services.AddTaxDeterminationProvider<StubExternalTaxProvider>());

        // A jurisdiction and a percentage rule exist, but the external provider takes over.
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Ignored",
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m));

        var line = Assert.Single(result.Lines);
        Assert.Equal("EXTERNAL", line.TaxCode);
        Assert.Equal(42m, result.TaxAmount);
    }

    private sealed class FlatTenTaxCalculationMethod : ITaxCalculationMethod
    {
        public const string MethodName = "FlatTen";

        public string Name => MethodName;

        public TaxComputationResult Compute(TaxComputationRequest request)
        {
            return new TaxComputationResult
            {
                TaxableAmount = request.TaxableBase,
                TaxAmount = 10m,
                EffectiveRate = 0m,
            };
        }
    }

    private sealed class StubExternalTaxProvider : ITaxDeterminationProvider
    {
        public int Order => 0;

        public bool CanHandle(TaxCalculationContext context) => true;

        public Task<TaxCalculationResult> DetermineAsync(TaxCalculationContext context, CancellationToken cancellationToken = default)
        {
            var result = new TaxCalculationResult
            {
                Currency = context.Currency,
                TaxableAmount = 100m,
                TaxAmount = 42m,
                TotalAmount = 142m,
                Lines =
                [
                    new TaxLine
                    {
                        TaxCode = "EXTERNAL",
                        TaxName = "External provider tax",
                        TaxAmount = 42m,
                        TaxableAmount = 100m,
                    },
                ],
            };

            return Task.FromResult(result);
        }
    }
}
