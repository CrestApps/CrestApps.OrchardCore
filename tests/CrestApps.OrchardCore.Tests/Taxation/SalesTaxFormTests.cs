using System.Globalization;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// Tax forms of the sales tax source as return forms: the generic return from Tax Returns' recipe, and a state's return
/// built the way the Tax Forms page builds one, with a discount for filing on time, a penalty and interest for filing
/// late, a check and its amount due field.
/// </summary>
public sealed class SalesTaxFormTests
{
    [Fact]
    public void GenericForm_TakesTaxableSales_FromGrossLessExempt_WhenNoLineGivesThem()
    {
        // Arrange
        var taxReturn = new TaxReturn
        {
            Lines =
            [
                new TaxReturnLine { Kind = TaxReturnLineKinds.GrossSales, Amount = 5000m },
                new TaxReturnLine { Kind = TaxReturnLineKinds.ExemptSales, Amount = 1200m },
                new TaxReturnLine { Kind = TaxReturnLineKinds.ExcessCollected, TaxAmount = 4m },
            ],
        };

        // Act
        SalesTaxFormRecipes.Compute(SalesTaxFormRecipes.GenericForm(), taxReturn);

        // Assert
        Assert.Equal((3800m, 0m, 4m, 4m), (taxReturn.Boxes["TaxableSales"], taxReturn.Boxes["SalesTax"], taxReturn.Boxes["TotalTax"], taxReturn.AmountDue));
        Assert.Equal(11, taxReturn.Boxes.Count);
    }

    [Fact]
    public void EditorBuiltForm_OnTime_TakesItsDiscount_AndReadsTheLinesByJurisdictionCategoryAndFormLine()
    {
        // Arrange
        var taxReturn = MonthlyReturn();

        // Act: a draft that is not late.
        SalesTaxFormRecipes.Compute(SalesTaxFormRecipes.MichiganStyleForm(), taxReturn);

        // Assert: Ohio's line is left out of line 4; 552 of tax gets a discount of 0.5%, 2.76.
        Assert.Equal((10000m, 1000m, 200m, 540m, 12m, 552m), (taxReturn.Boxes["1"], taxReturn.Boxes["2"], taxReturn.Boxes["3"], taxReturn.Boxes["4"], taxReturn.Boxes["5"], taxReturn.Boxes["6"]));
        Assert.Equal((2.76m, 0m, 0m, 20m), (taxReturn.Boxes["7"], taxReturn.Boxes["8"], taxReturn.Boxes["9"], taxReturn.Boxes["10"]));
        Assert.Equal(529.24m, taxReturn.AmountDue);
        Assert.Empty(taxReturn.Notes);
    }

    [Fact]
    public void EditorBuiltForm_FiledLate_LosesItsDiscount_AndOwesPenaltyAndInterest()
    {
        // Arrange
        var taxReturn = MonthlyReturn();

        // Act: due 20 November, filed 4 January, 45 days late: a month and part of another.
        SalesTaxFormRecipes.Compute(SalesTaxFormRecipes.MichiganStyleForm(), taxReturn, new DateOnly(2027, 1, 4));

        // Assert: 5% of 552 for two months is 55.20; 0.01% a day for 45 days is 2.48.
        Assert.Equal((0m, 55.20m, 2.48m), (taxReturn.Boxes["7"], taxReturn.Boxes["8"], taxReturn.Boxes["9"]));
        Assert.Equal(589.68m, taxReturn.AmountDue);
    }

    [Fact]
    public void EditorBuiltForm_ACheckThatFails_IsANoteOfTheReturn()
    {
        // Arrange
        var taxReturn = MonthlyReturn();

        taxReturn.RegistrationNumber = "MI-0001";

        // Act
        SalesTaxFormRecipes.Compute(SalesTaxFormRecipes.MichiganStyleForm(), taxReturn);

        // Assert
        Assert.Equal(["The registration number is not a Michigan account number."], taxReturn.Notes);
    }

    [Fact]
    public async Task FormWhoseFormulasDontWork_LeavesANote_AndNoBoxes()
    {
        // Arrange
        var form = SalesTaxFormRecipes.MichiganStyleForm();
        var taxReturn = MonthlyReturn();

        form.Fields[0].Formula = "NOSUCHFUNCTION()";

        // Act
        await new TaxFormReturnForm(form, SalesTaxFormRecipes.Functions(), new PassThroughStringLocalizer<TaxFormReturnForm>()).ComputeAsync(new TaxReturnFormContext
        {
            Registration = new MerchantTaxRegistration(),
            Return = taxReturn,
            Services = new ServiceCollection().BuildServiceProvider(),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(taxReturn.Boxes);
        Assert.Equal(0m, taxReturn.AmountDue);
        Assert.Contains(taxReturn.Notes, note => note.Contains("US-MI-TEST", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("2026-10-20", null, 0, 0)]
    [InlineData("2026-10-20", "2026-10-20", 0, 0)]
    [InlineData("2026-10-20", "2026-11-02", 13, 1)]
    [InlineData("2026-10-20", "2026-11-20", 31, 1)]
    [InlineData("2026-10-20", "2026-11-21", 32, 2)]
    [InlineData("2026-01-31", "2026-03-01", 29, 2)]
    public void DaysAndMonthsLate_CountAMonthBegunAsAWholeOne(string due, string filed, int days, int months)
    {
        // Arrange
        var data = new SalesTaxFormData
        {
            Return = new TaxReturn { DueDate = DateOnly.Parse(due, CultureInfo.InvariantCulture) },
            Registration = new MerchantTaxRegistration(),
            FiledOn = filed is null ? null : DateOnly.Parse(filed, CultureInfo.InvariantCulture),
        };

        // Act & Assert
        Assert.Equal((days, months), (data.DaysLate, data.MonthsLate));
    }

    [Theory]
    [InlineData("us-*", 570)]
    [InlineData("US-MI", 540)]
    [InlineData("*", 570)]
    [InlineData("", 570)]
    [InlineData("ca-*", 0)]
    public void Lines_MatchTheirJurisdiction_TheReturnsOwnWhenTheyHaveNone(string jurisdiction, decimal tax)
        => Assert.Equal(tax, SalesTaxFormFunctions.Match(MonthlyReturn(), TaxReturnLineKinds.TaxableSales, jurisdiction).Sum(line => line.TaxAmount));

    [Fact]
    public void Functions_AreOnlyForSalesTaxForms()
    {
        // Act
        var functions = SalesTaxFormRecipes.Functions();

        // Assert
        Assert.Contains(functions.For(TaxationConstants.SalesTaxFormSource).Functions, function => function.Name == "MONTHSLATE");
        Assert.DoesNotContain(functions.For("Payroll").Functions, function => function.Name == "MONTHSLATE");
    }

    // October 2026 of a Michigan registration, due 20 November, with an Ohio line a multistate provider gave too.
    private static TaxReturn MonthlyReturn()
        => new()
        {
            JurisdictionId = "us-mi",
            RegistrationNumber = "12-3456789",
            Frequency = TaxFilingFrequencies.Monthly,
            PeriodStart = new DateOnly(2026, 10, 1),
            PeriodEnd = new DateOnly(2026, 10, 31),
            DueDate = new DateOnly(2026, 11, 20),
            Lines =
            [
                new TaxReturnLine { Kind = TaxReturnLineKinds.GrossSales, Amount = 10000m },
                new TaxReturnLine { Kind = TaxReturnLineKinds.ExemptSales, Amount = 1000m, Category = "Resale" },
                new TaxReturnLine { Kind = TaxReturnLineKinds.ExemptSales, Amount = 300m, Category = "Food" },
                new TaxReturnLine { Kind = TaxReturnLineKinds.TaxableSales, Amount = 9000m, TaxAmount = 540m, TaxType = TaxTypeNames.SalesTax },
                new TaxReturnLine { Kind = TaxReturnLineKinds.TaxableSales, JurisdictionId = "us-oh", Amount = 500m, TaxAmount = 30m, TaxType = TaxTypeNames.SalesTax },
                new TaxReturnLine { Kind = TaxReturnLineKinds.UseTaxPurchases, Amount = 200m, TaxAmount = 12m, TaxType = TaxTypeNames.UseTax, FormLine = "3" },
                new TaxReturnLine { Kind = TaxReturnLineKinds.Credit, TaxAmount = 20m },
            ],
        };
}
