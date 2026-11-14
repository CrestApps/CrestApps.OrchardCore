using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Gross-to-net cases, one file each under <c>Payroll/Golden</c>: the setup of a pay (codes, rates, inputs, items, tax
/// rules, accumulators, the split of net pay) and every line, payment and total the engine must produce. The expected
/// amounts were worked out by hand from the rules each file states.
/// </summary>
public sealed class PayEngineGoldenTests
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();

        foreach (var file in Directory.GetFiles(GoldenDirectory(), "*.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Engine_ProducesTheGoldenStatement(string name)
    {
        var golden = JsonSerializer.Deserialize<GoldenCase>(await File.ReadAllTextAsync(Path.Combine(GoldenDirectory(), name + ".json"), TestContext.Current.CancellationToken), _options);
        var context = Build(golden);

        await Calculator(golden).CalculateAsync(context, TestContext.Current.CancellationToken);

        var actual = context.Lines.Select(line => Describe(context, line)).ToList();

        Assert.Equal(golden.Expected.Lines.Select(line => line.ToString()), actual);
        Assert.Equal(golden.Expected.Gross, context.Gross);
        Assert.Equal(golden.Expected.Net, context.Round(context.Net));
        Assert.Equal(golden.Expected.Payments.Select(payment => $"{payment.Method} {payment.BankAccountId} {payment.Amount:0.00}"), context.Payments.Select(payment => $"{payment.Method} {payment.BankAccountId} {payment.Amount:0.00}"));

        foreach (var warning in golden.Expected.WarningsContaining)
        {
            Assert.Contains(context.Warnings, actualWarning => actualWarning.Contains(warning, StringComparison.Ordinal));
        }

        if (golden.Expected.WarningsContaining.Count == 0)
        {
            Assert.Empty(context.Warnings);
        }
    }

    private static string Describe(PayCalculationContext context, PayStatementLine line)
    {
        var key = line.Type == PayLineType.Tax ? $"{line.TaxCode} {line.Payer}" : context.Code(line.PayCodeId)?.Code;
        var extra = line.Type == PayLineType.Tax ? $" on {line.TaxableWages:0.00}" : line.Hours is { } hours ? $" for {hours:0.##}h" : string.Empty;
        var cut = line.Cut > 0 ? $" cut {line.Cut:0.00}" : string.Empty;
        var arrears = line.ArrearsAdded > 0 ? $" owed {line.ArrearsAdded:0.00}" : string.Empty;

        return $"{line.Type} {key} {line.Amount:0.00}{extra}{cut}{arrears}";
    }

    private static PayCalculationContext Build(GoldenCase golden)
    {
        var codes = golden.Codes.Select(seed =>
        {
            var code = new PayCode
            {
                ItemId = seed.Code,
                Code = seed.Code,
                Name = seed.Name ?? seed.Code,
                Class = seed.Class,
                Kind = seed.Kind ?? PayCodeKinds.Regular,
                Multiplier = seed.Multiplier ?? 1,
                PremiumPerHour = seed.PremiumPerHour,
            };

            if (seed.Settings is not null)
            {
                code.Put(seed.Settings);
            }

            return code;
        }).ToDictionary(code => code.ItemId);

        var profile = new EmployeePayProfile
        {
            ItemId = "profile",
            StaffMemberId = "staff",
            EmployerId = "employer",
            WorkerType = golden.WorkerType,
            OvertimeStatus = golden.OvertimeStatus,
            Elections = golden.Elections,
            Distribution = golden.Distribution,
        };

        var context = new PayCalculationContext
        {
            Profile = profile,
            Employer = new Employer { ItemId = "employer", Name = "Contoso", Currency = golden.Currency, Registrations = [.. golden.Registrations.Select(jurisdiction => new EmployerTaxRegistration { Jurisdiction = jurisdiction })] },
            Schedule = new PaySchedule { Frequency = golden.Frequency, WeekStart = DayOfWeek.Sunday },
            PeriodStart = golden.PeriodStart,
            PeriodEnd = golden.PeriodEnd,
            PayDate = golden.PayDate,
            Pay = golden.Pay ?? new StaffPayRates(),
            HireDate = golden.HireDate,
            TerminationDate = golden.TerminationDate,
            Codes = codes,
            IncludeSalary = golden.IncludeSalary,
            SupplementalMethod = golden.SupplementalMethod,
            MinimumNet = golden.MinimumNet,
            HomeJurisdictions = golden.HomeJurisdictions,
            WorkJurisdictions = golden.WorkJurisdictions,
            Inputs = [.. golden.Inputs.Select(input => new PayInput
            {
                SourceKind = "Golden",
                SourceId = "input",
                StaffMemberId = "staff",
                PayCodeId = input.PayCode,
                Date = input.Date,
                Hours = input.Hours,
                Rate = input.Rate,
                Amount = input.Amount,
                PeriodStart = golden.PeriodStart,
                PeriodEnd = golden.PeriodEnd,
            })],
            Items = [.. golden.Items.Select((item, index) => new EmployeePayItem
            {
                ItemId = "item" + index,
                ProfileId = "profile",
                PayCodeId = item.PayCode,
                Amount = item.Amount,
                Percent = item.Percent,
                StartDate = item.StartDate ?? golden.PeriodStart.AddYears(-1),
                EndDate = item.EndDate,
                Goal = item.Goal,
                PaidTowardGoal = item.PaidTowardGoal,
                PeriodMaximum = item.PeriodMaximum,
                YearMaximum = item.YearMaximum,
                EmployerMatchPercent = item.EmployerMatchPercent,
                EmployerMatchLimitPercent = item.EmployerMatchLimitPercent,
                ArrearsBalance = item.ArrearsBalance,
            })],
        };

        foreach (var (key, totals) in golden.Accumulators)
        {
            context.Accumulators.Totals[key] = totals;
        }

        if (golden.GrossUp is { } grossUp)
        {
            context.GrossUp = new PayGrossUp { PayCodeId = grossUp.PayCode, TargetNet = grossUp.TargetNet };
        }

        return context;
    }

    private static PayCalculator Calculator(GoldenCase golden)
    {
        var categories = new PayrollTaxCategoryOptions();

        categories.Categories.Add(new PayrollTaxCategory(PayrollTaxCategories.IncomeTax, LocalizationSource.Create<PayEngineGoldenTests>("Income tax")));
        categories.Categories.Add(new PayrollTaxCategory(PayrollTaxCategories.SocialInsurance, LocalizationSource.Create<PayEngineGoldenTests>("Social insurance")));
        categories.Categories.Add(new PayrollTaxCategory(PayrollTaxCategories.Unemployment, LocalizationSource.Create<PayEngineGoldenTests>("Unemployment")));

        var tables = new InMemoryNamedCatalog<TaxTable>();
        var rules = new List<PayrollTaxRule>();

        foreach (var (rule, index) in golden.TaxRules.Select((rule, index) => (rule, index)))
        {
            if (rule.Table is { Count: > 0 } rows)
            {
                rule.Rule.TaxTableId = "table" + index;
                tables.CreateAsync(new TaxTable { ItemId = rule.Rule.TaxTableId, Name = rule.Rule.TaxTableId, Rows = rows }).AsTask().GetAwaiter().GetResult();
            }

            rule.Rule.ItemId = "rule" + index;
            rule.Rule.Enabled = true;
            rules.Add(rule.Rule);
        }

        ITaxCalculationMethod[] methods =
        [
            new PercentageTaxCalculationMethod(), new FixedAmountTaxCalculationMethod(), new TaxTableTaxCalculationMethod(),
            new ProgressiveTaxCalculationMethod(), new ThresholdTaxCalculationMethod(),
        ];
        var ruleManager = new Moq.Mock<IPayrollTaxRuleManager>();

        ruleManager.Setup(manager => manager.ListAsync(Moq.It.IsAny<CancellationToken>())).ReturnsAsync(rules);

        var provider = new TaxTablesPayrollTaxProvider(ruleManager.Object, new DefaultTaxCalculationMethodProvider(methods), tables, new PassThroughStringLocalizer<TaxTablesPayrollTaxProvider>());
        var services = new ServiceCollection().BuildServiceProvider();

        return new PayCalculator(
        [
            new EarningsPayCalculationStep(new PassThroughStringLocalizer<EarningsPayCalculationStep>()),
            new RegularRatePayCalculationStep(new PassThroughStringLocalizer<RegularRatePayCalculationStep>()),
            new PreTaxDeductionsPayCalculationStep(),
            new TaxesPayCalculationStep(golden.NoTaxProvider ? [] : [provider], Options.Create(categories), services, new PassThroughStringLocalizer<TaxesPayCalculationStep>()),
            new PostTaxDeductionsPayCalculationStep(),
            new DisposableEarningsPayCalculationStep(),
            new NetCheckPayCalculationStep(new PassThroughStringLocalizer<NetCheckPayCalculationStep>()),
            new ContributionsPayCalculationStep(new PassThroughStringLocalizer<ContributionsPayCalculationStep>()),
            new DistributionPayCalculationStep(new PassThroughStringLocalizer<DistributionPayCalculationStep>()),
        ], new PassThroughStringLocalizer<PayCalculator>());
    }

    private static string GoldenDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The repository root was not found."), "tests", "CrestApps.OrchardCore.Tests", "Payroll", "Golden");
    }

    private sealed class GoldenCase
    {
        public string Description { get; set; }

        public string Currency { get; set; } = "USD";

        public PayFrequency Frequency { get; set; } = PayFrequency.Biweekly;

        public DateOnly PeriodStart { get; set; }

        public DateOnly PeriodEnd { get; set; }

        public DateOnly PayDate { get; set; }

        public PayWorkerType WorkerType { get; set; }

        public OvertimeStatus OvertimeStatus { get; set; }

        public StaffPayRates Pay { get; set; }

        public DateOnly? HireDate { get; set; }

        public DateOnly? TerminationDate { get; set; }

        public bool IncludeSalary { get; set; } = true;

        public PayrollSupplementalMethod SupplementalMethod { get; set; }

        public decimal MinimumNet { get; set; }

        public bool NoTaxProvider { get; set; }

        public List<string> Registrations { get; set; } = ["US"];

        public List<string> HomeJurisdictions { get; set; } = [];

        public List<string> WorkJurisdictions { get; set; } = [];

        public List<TaxElection> Elections { get; set; } = [];

        public List<NetPayRow> Distribution { get; set; } = [];

        public List<GoldenCode> Codes { get; set; } = [];

        public List<GoldenInput> Inputs { get; set; } = [];

        public List<GoldenItem> Items { get; set; } = [];

        public List<GoldenRule> TaxRules { get; set; } = [];

        public Dictionary<string, PayAccumulator> Accumulators { get; set; } = [];

        public GoldenGrossUp GrossUp { get; set; }

        public GoldenExpected Expected { get; set; }
    }

    private sealed class GoldenCode
    {
        public string Code { get; set; }

        public string Name { get; set; }

        public PayCodeClass Class { get; set; }

        public string Kind { get; set; }

        public decimal? Multiplier { get; set; }

        public decimal? PremiumPerHour { get; set; }

        public PayrollPayCodeSettings Settings { get; set; }
    }

    private sealed class GoldenInput
    {
        public string PayCode { get; set; }

        public DateOnly? Date { get; set; }

        public decimal? Hours { get; set; }

        public decimal? Rate { get; set; }

        public decimal? Amount { get; set; }
    }

    private sealed class GoldenItem
    {
        public string PayCode { get; set; }

        public decimal? Amount { get; set; }

        public decimal? Percent { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        public decimal? Goal { get; set; }

        public decimal PaidTowardGoal { get; set; }

        public decimal? PeriodMaximum { get; set; }

        public decimal? YearMaximum { get; set; }

        public decimal? EmployerMatchPercent { get; set; }

        public decimal? EmployerMatchLimitPercent { get; set; }

        public decimal ArrearsBalance { get; set; }
    }

    private sealed class GoldenRule
    {
        [JsonPropertyName("rule")]
        public PayrollTaxRule Rule { get; set; }

        public List<TaxTableRow> Table { get; set; }
    }

    private sealed class GoldenGrossUp
    {
        public string PayCode { get; set; }

        public decimal TargetNet { get; set; }
    }

    private sealed class GoldenExpected
    {
        public List<string> Lines { get; set; } = [];

        public decimal Gross { get; set; }

        public decimal Net { get; set; }

        public List<NetPayRow> Payments { get; set; } = [];

        public List<string> WarningsContaining { get; set; } = [];
    }
}
