using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxCalculationMethodInputsTests
{
    public static TheoryData<ITaxCalculationMethod, TaxCalculationMethodInputs> Methods => new()
    {
        { new PercentageTaxCalculationMethod(), TaxCalculationMethodInputs.Rate },
        { new FixedAmountTaxCalculationMethod(), TaxCalculationMethodInputs.FixedAmount },
        { new PerUnitTaxCalculationMethod(), TaxCalculationMethodInputs.FixedAmount },
        { new PerWeightTaxCalculationMethod(), TaxCalculationMethodInputs.FixedAmount },
        { new PerVolumeTaxCalculationMethod(), TaxCalculationMethodInputs.FixedAmount },
        { new ProgressiveTaxCalculationMethod(), TaxCalculationMethodInputs.TaxTable },
        { new ThresholdTaxCalculationMethod(), TaxCalculationMethodInputs.TaxTable },
        { new TaxTableTaxCalculationMethod(), TaxCalculationMethodInputs.TaxTable },
    };

    [Theory]
    [MemberData(nameof(Methods))]
    public void CalculationMethod_DeclaresExpectedInputs(ITaxCalculationMethod method, TaxCalculationMethodInputs expected)
    {
        // Act
        var inputs = method.Inputs;

        // Assert
        Assert.Equal(expected, inputs);
    }
}
