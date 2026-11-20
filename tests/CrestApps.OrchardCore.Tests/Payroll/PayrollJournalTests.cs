using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// The payroll journal: each line of a final statement as a balanced debit and credit on the accounts of the settings.
/// </summary>
public sealed class PayrollJournalTests
{
    private static readonly PayrollJournalSettings _settings = new()
    {
        NetPayAccount = "2100",
        WageExpenseAccount = "6000",
        DeductionLiabilityAccount = "2200",
        TaxLiabilityAccount = "2300",
        EmployerTaxExpenseAccount = "6100",
        ContributionExpenseAccount = "6200",
        ContributionLiabilityAccount = "2400",
        ReimbursementExpenseAccount = "6300",
    };

    private static readonly Dictionary<string, PayCode> _codes = new(StringComparer.Ordinal)
    {
        ["reg"] = new PayCode { ItemId = "reg", Code = "REG" },
        ["ins"] = new PayCode { ItemId = "ins", Code = "INS" },
        ["k401"] = new PayCode { ItemId = "k401", Code = "ER401K" },
        ["mil"] = new PayCode { ItemId = "mil", Code = "MILE" },
        ["gtl"] = new PayCode { ItemId = "gtl", Code = "GTL" },
    };

    private static PayStatement Statement(decimal sign = 1)
        => new()
        {
            ItemId = "st1",
            Number = "1001",
            RunId = "run1",
            EmployerId = "contoso",
            PayDate = new DateOnly(2026, 11, 20),
            Currency = "USD",
            Lines =
            [
                new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Description = "Regular", Amount = sign * 1000, Allocation = new PayInputAllocation { DepartmentTeamId = "sales" } },
                new PayStatementLine { Type = PayLineType.Deduction, PayCodeId = "ins", Description = "Insurance", Amount = sign * 50 },
                new PayStatementLine { Type = PayLineType.Tax, TaxCode = "US-FIT", Description = "Federal income tax", Payer = PayLinePayer.Employee, Amount = sign * 100 },
                new PayStatementLine { Type = PayLineType.Tax, TaxCode = "US-SS-ER", Description = "Social Security (employer)", Payer = PayLinePayer.Employer, Amount = sign * 62 },
                new PayStatementLine { Type = PayLineType.EmployerContribution, PayCodeId = "k401", Description = "401(k) match", Amount = sign * 30 },
                new PayStatementLine { Type = PayLineType.Reimbursement, PayCodeId = "mil", Description = "Mileage", Amount = sign * 20 },
                new PayStatementLine { Type = PayLineType.ImputedIncome, PayCodeId = "gtl", Description = "Group term life", Amount = sign * 10 },
                new PayStatementLine { Type = PayLineType.Memo, Description = "Memo", Amount = sign * 5 },
            ],
        };

    [Fact]
    public void Entries_BalanceAndUseTheAccountOfEachKind_AndSkipLinesThatMoveNoMoney()
    {
        var entries = PayrollJournal.EntriesFor(Statement(), _codes, _settings, team => team == "sales" ? "D-100" : null).ToList();

        // Six lines with money, two entries each; imputed income and the memo have none.
        Assert.Equal(12, entries.Count);
        Assert.Equal(entries.Sum(entry => entry.Debit), entries.Sum(entry => entry.Credit));

        decimal Balance(string account)
            => entries.Where(entry => entry.Account == account).Sum(entry => entry.Debit - entry.Credit);

        // Net pay owed: 1000 + 20 credited, 50 + 100 debited, so 870 is owed, the statement's net pay.
        Assert.Equal(-870m, Balance("2100"));
        Assert.Equal(1000m, Balance("6000"));
        Assert.Equal(-50m, Balance("2200"));
        Assert.Equal(-162m, Balance("2300"));
        Assert.Equal(62m, Balance("6100"));
        Assert.Equal(30m, Balance("6200"));
        Assert.Equal(-30m, Balance("2400"));
        Assert.Equal(20m, Balance("6300"));

        // The earning's department goes with both of its entries; lines without one have none.
        Assert.All(entries.Where(entry => entry.Source == "REG"), entry => Assert.Equal("D-100", entry.Department));
        Assert.All(entries.Where(entry => entry.Source != "REG"), entry => Assert.Null(entry.Department));
        Assert.All(entries, entry => Assert.Equal((new DateOnly(2026, 11, 20), "USD", "st1"), (entry.PayDate, entry.Currency, entry.StatementId)));
        Assert.DoesNotContain(entries, entry => entry.Source == "GTL");
    }

    [Fact]
    public void Mappings_ReplaceTheAccountsOfAPayCodeOrATax_AndAnEmptySideKeepsTheDefault()
    {
        var settings = new PayrollJournalSettings
        {
            NetPayAccount = _settings.NetPayAccount,
            WageExpenseAccount = _settings.WageExpenseAccount,
            TaxLiabilityAccount = _settings.TaxLiabilityAccount,
            Mappings =
            [
                new PayrollJournalMapping { Key = "reg", DebitAccount = "6010" },
                new PayrollJournalMapping { Key = "US-FIT", CreditAccount = " 2310 " },
            ],
        };

        var entries = PayrollJournal.EntriesFor(Statement(), _codes, settings).ToList();

        Assert.Equal(["6010", "2100"], entries.Where(entry => entry.Source == "REG").Select(entry => entry.Account));
        Assert.Equal(["2100", "2310"], entries.Where(entry => entry.Source == "US-FIT").Select(entry => entry.Account));
    }

    [Fact]
    public void AReversingStatement_SwapsTheSides_SoAVoidCancelsItsEntries()
    {
        var original = PayrollJournal.EntriesFor(Statement(), _codes, _settings).ToList();
        var reversal = PayrollJournal.EntriesFor(Statement(sign: -1), _codes, _settings).ToList();

        Assert.All(reversal, entry => Assert.True(entry.Debit >= 0 && entry.Credit >= 0));
        Assert.Equal(reversal.Sum(entry => entry.Debit), reversal.Sum(entry => entry.Credit));

        foreach (var account in original.Select(entry => entry.Account).Distinct())
        {
            Assert.Equal(0m, original.Concat(reversal).Where(entry => entry.Account == account).Sum(entry => entry.Debit - entry.Credit));
        }
    }

    [Fact]
    public void ShippedRecipe_GroupsJournalEntriesByFieldsTheDataSetHas_AndDeliversNothingByItself()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "src", "Modules", "CrestApps.OrchardCore.Payroll", "Recipes", "payroll-journal-export.recipe.json")))!.AsObject();
        var pipeline = recipe["steps"]![1]!["Pipelines"]![0]!.AsObject();
        var steps = pipeline["Definition"]!["Steps"]!.AsArray().Select(step => step!.AsObject()).ToList();
        var dataSet = new JournalEntriesDataSet(null, Mock.Of<IPayCodeManager>(), null, Mock.Of<IServiceProvider>(), RecipeRoundTrip.Localizer<JournalEntriesDataSet>());
        var fields = dataSet.Fields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        var source = steps.Single(step => step["Type"]!.GetValue<string>() == "DataSource")["Properties"]!["DataSourceStepSettings"]!;
        var read = source["Fields"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();
        var aggregate = steps.Single(step => step["Type"]!.GetValue<string>() == "Aggregate")["Properties"]!["AggregateStepSettings"]!;

        Assert.Equal((PayrollConstants.DataSourceName, "JournalEntries"), (source["Source"]!.GetValue<string>(), source["DataSet"]!.GetValue<string>()));
        Assert.All(read, field => Assert.Contains(field, fields));
        Assert.All(aggregate["GroupBy"]!.AsArray().Select(node => node!.GetValue<string>()), field => Assert.Contains(field, read));
        Assert.Equal(["Debit", "Credit"], aggregate["Measures"]!.AsArray().Select(node => node!["Field"]!.GetValue<string>()));

        // A draft that writes a file: the business adds the delivery its accounting system takes.
        Assert.Equal(["DataSource", "Aggregate", "Sort", "CreateFile"], steps.Select(step => step["Type"]!.GetValue<string>()));
    }
}
