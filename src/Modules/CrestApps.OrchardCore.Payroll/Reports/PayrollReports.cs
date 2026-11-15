using System.Globalization;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Payroll.Reports;

/// <summary>
/// What Payroll's reports share: the date range read as pay dates in the user's time zone, the employers the user may
/// see, money with its currency, and an empty report that says so.
/// </summary>
public abstract class PayrollReportBase
{
    protected PayrollReportBase(PayrollReportReader reader, ILocalClock localClock, IStringLocalizer stringLocalizer)
    {
        Reader = reader;
        LocalClock = localClock;
        S = stringLocalizer;
    }

    protected PayrollReportReader Reader { get; }

    protected ILocalClock LocalClock { get; }

    protected IStringLocalizer S { get; }

    // The pay dates of the date range: pure dates, read in the user's time zone.
    protected async Task<(PayrollScope Scope, DateOnly? From, DateOnly? To)> ReadAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var range = context.Filter.GetDateRange();

        // On screen, the employers whose runs the user sees; in a file (an export or an attachment), only those of them
        // whose payroll data the user may also export.
        var scope = await Reader.GetReportScopeAsync(context, PayrollPermissions.ViewPayRuns, cancellationToken);

        if (context.Mode == ReportRunMode.Export)
        {
            scope = PayrollReportReader.Intersect(scope, await Reader.GetReportScopeAsync(context, PayrollPermissions.ExportPayrollData, cancellationToken));
        }

        return (
            scope,
            range.FromUtc is { } from ? DateOnly.FromDateTime((await LocalClock.ConvertToLocalAsync(new DateTimeOffset(from, TimeSpan.Zero))).DateTime) : null,
            range.ToUtc is { } to ? DateOnly.FromDateTime((await LocalClock.ConvertToLocalAsync(new DateTimeOffset(to, TimeSpan.Zero))).DateTime) : null);
    }

    protected static string Money(decimal amount, string currency)
        => string.IsNullOrEmpty(currency) ? amount.ToString("N2", CultureInfo.CurrentCulture) : CurrencyScale.Format(amount, currency) + " " + currency;

    protected static string Day(DateOnly day, ReportContext context)
        => context.Mode == ReportRunMode.Export ? day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : day.ToString("d", CultureInfo.CurrentCulture);

    protected static ReportDocument Empty(ReportContext context, string title, string message)
    {
        if (context.Mode == ReportRunMode.Export)
        {
            return new ReportDocument();
        }

        var section = ReportSection.ForTable(title, [], []);
        section.EmptyMessage = message;

        return new ReportDocument().Add(section);
    }

    protected static decimal Sum(PayStatement statement, Func<PayStatementLine, bool> predicate)
        => statement.Lines.Where(predicate).Sum(line => line.Amount);

    protected static bool IsEmployeeTax(PayStatementLine line)
        => line.Type == PayLineType.Tax && line.Payer == PayLinePayer.Employee;

    protected static bool IsEmployerTax(PayStatementLine line)
        => line.Type == PayLineType.Tax && line.Payer == PayLinePayer.Employer;
}

/// <summary>
/// The payroll register: each final statement of the period with gross, taxes, deductions and net pay.
/// </summary>
public sealed class PayrollRegisterReport : PayrollReportBase, IReport
{
    public PayrollRegisterReport(PayrollReportReader reader, ILocalClock localClock, IStringLocalizer<PayrollRegisterReport> stringLocalizer)
        : base(reader, localClock, stringLocalizer)
    {
    }

    public static string Name => "payroll-register";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<PayrollRegisterReport>("Payroll Register");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<PayrollRegisterReport>("Each pay statement paid in the period: gross pay, the person's taxes, deductions and net pay, with totals.");

    public static string Category => ReportsConstants.Categories.WorkforcePayroll;

    public static Permission Permission => PayrollPermissions.ViewPayRuns;

    public static Permission ExportPermission => PayrollPermissions.ExportPayrollData;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (scope, from, to) = await ReadAsync(context, cancellationToken);
        var statements = (await Reader.ListStatementsAsync(scope, from, to, cancellationToken)).OrderBy(statement => statement.PayDate).ThenBy(statement => statement.Snapshot?.EmployeeName, StringComparer.CurrentCultureIgnoreCase).ToList();
        var title = S["Payroll register"].Value;

        if (statements.Count == 0)
        {
            return Empty(context, title, S["No pay statement was paid in these days."].Value);
        }

        var columns = new List<ReportColumn>
        {
            new(S["Pay date"].Value), new(S["Person"].Value), new(S["Statement"].Value),
            new(S["Gross"].Value, ReportColumnAlign.End), new(S["Taxes"].Value, ReportColumnAlign.End),
            new(S["Deductions"].Value, ReportColumnAlign.End), new(S["Net pay"].Value, ReportColumnAlign.End),
        };
        var rows = statements.Select(statement => new ReportRow(
        [
            Day(statement.PayDate, context),
            statement.Snapshot?.EmployeeName ?? statement.StaffMemberId,
            statement.VoidOf is null ? statement.Number : S["{0} (reversal)", statement.Number].Value,
            Money(statement.Gross, statement.Currency),
            Money(Sum(statement, IsEmployeeTax), statement.Currency),
            Money(Sum(statement, line => line.Type == PayLineType.Deduction), statement.Currency),
            Money(statement.Net, statement.Currency),
        ])).ToList();

        foreach (var currency in statements.GroupBy(statement => statement.Currency))
        {
            rows.Add(new ReportRow(
            [
                S["Total"].Value, string.Empty, string.Empty,
                Money(currency.Sum(statement => statement.Gross), currency.Key),
                Money(currency.Sum(statement => Sum(statement, IsEmployeeTax)), currency.Key),
                Money(currency.Sum(statement => Sum(statement, line => line.Type == PayLineType.Deduction)), currency.Key),
                Money(currency.Sum(statement => statement.Net), currency.Key),
            ], ReportRowKind.GrandTotal));
        }

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Statements"].Value, ReportFormat.Number(statements.Count(statement => statement.VoidOf is null))),
                new ReportMetric(S["People paid"].Value, ReportFormat.Number(statements.Select(statement => statement.StaffMemberId).Distinct(StringComparer.Ordinal).Count())),
            ]))
            .Add(ReportSection.ForTable(title, columns, rows));
    }
}

/// <summary>
/// The payroll summary: the period's total of each pay code and tax.
/// </summary>
public sealed class PayrollSummaryReport : PayrollReportBase, IReport
{
    public PayrollSummaryReport(PayrollReportReader reader, ILocalClock localClock, IStringLocalizer<PayrollSummaryReport> stringLocalizer)
        : base(reader, localClock, stringLocalizer)
    {
    }

    public static string Name => "payroll-summary";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<PayrollSummaryReport>("Payroll Summary");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<PayrollSummaryReport>("The total of each earning, deduction, tax and contribution of the pay statements paid in the period.");

    public static string Category => ReportsConstants.Categories.WorkforcePayroll;

    public static Permission Permission => PayrollPermissions.ViewPayRuns;

    public static Permission ExportPermission => PayrollPermissions.ExportPayrollData;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (scope, from, to) = await ReadAsync(context, cancellationToken);
        var statements = await Reader.ListStatementsAsync(scope, from, to, cancellationToken);
        var title = S["Payroll summary"].Value;

        if (statements.Count == 0)
        {
            return Empty(context, title, S["No pay statement was paid in these days."].Value);
        }

        var rows = statements
            .SelectMany(statement => statement.Lines.Select(line => (statement.Currency, Line: line)))
            .GroupBy(entry => (entry.Line.Type, entry.Line.Payer, Name: entry.Line.Description ?? entry.Line.TaxCode ?? entry.Line.PayCodeId, entry.Currency))
            .OrderBy(group => group.Key.Type)
            .ThenBy(group => group.Key.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new ReportRow(
            [
                group.Key.Name,
                group.Key.Type == PayLineType.Tax ? (group.Key.Payer == PayLinePayer.Employer ? S["Employer's tax"].Value : S["Person's tax"].Value) : group.Key.Type.ToString(),
                group.Sum(entry => entry.Line.Hours ?? 0) is var hours && hours != 0 ? hours.ToString("0.##", CultureInfo.CurrentCulture) : string.Empty,
                Money(group.Sum(entry => entry.Line.Amount), group.Key.Currency),
            ]))
            .ToList();

        return new ReportDocument()
            .Add(ReportSection.ForTable(title, [new(S["Line"].Value), new(S["Type"].Value), new(S["Hours"].Value, ReportColumnAlign.End), new(S["Amount"].Value, ReportColumnAlign.End)], rows));
    }
}

/// <summary>
/// The cash requirement: what each run of the period needs in the bank, by pay date: net pay, the employer's taxes and
/// contributions.
/// </summary>
public sealed class CashRequirementReport : PayrollReportBase, IReport
{
    public CashRequirementReport(PayrollReportReader reader, ILocalClock localClock, IStringLocalizer<CashRequirementReport> stringLocalizer)
        : base(reader, localClock, stringLocalizer)
    {
    }

    public static string Name => "payroll-cash-requirement";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<CashRequirementReport>("Cash Requirement");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<CashRequirementReport>("The cash each pay run needs in the employer's bank by its pay date: net pay, the employer's taxes and contributions.");

    public static string Category => ReportsConstants.Categories.WorkforcePayroll;

    public static Permission Permission => PayrollPermissions.ViewPayRuns;

    public static Permission ExportPermission => PayrollPermissions.ExportPayrollData;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (scope, from, to) = await ReadAsync(context, cancellationToken);
        var employers = await Reader.EmployerNamesAsync(cancellationToken);
        var runs = (await Reader.ListRunsAsync(scope, from, to, cancellationToken))
            .Where(run => run.Status is not (PayRunStatus.Canceled or PayRunStatus.Draft))
            .OrderBy(run => run.PayDate)
            .ToList();
        var title = S["Cash requirement"].Value;

        if (runs.Count == 0)
        {
            return Empty(context, title, S["No pay run is paid in these days."].Value);
        }

        var rows = runs.Select(run => new ReportRow(
        [
            Day(run.PayDate, context),
            employers.GetValueOrDefault(run.EmployerId) ?? run.EmployerId,
            run.Number,
            run.Status.ToString(),
            Money(run.Totals.Net, null),
            Money(run.Totals.EmployerTaxes, null),
            Money(run.Totals.EmployerContributions, null),
            Money(run.Totals.CashNeeded, null),
        ])).ToList();

        rows.Add(new ReportRow([S["Total"].Value, string.Empty, string.Empty, string.Empty, Money(runs.Sum(run => run.Totals.Net), null), Money(runs.Sum(run => run.Totals.EmployerTaxes), null), Money(runs.Sum(run => run.Totals.EmployerContributions), null), Money(runs.Sum(run => run.Totals.CashNeeded), null)], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Pay runs"].Value, ReportFormat.Number(runs.Count)),
                new ReportMetric(S["Cash needed"].Value, Money(runs.Sum(run => run.Totals.CashNeeded), null)),
            ]))
            .Add(ReportSection.ForTable(title,
            [
                new(S["Pay date"].Value), new(S["Employer"].Value), new(S["Run"].Value), new(S["Status"].Value),
                new(S["Net pay"].Value, ReportColumnAlign.End), new(S["Employer's taxes"].Value, ReportColumnAlign.End),
                new(S["Employer contributions"].Value, ReportColumnAlign.End), new(S["Cash needed"].Value, ReportColumnAlign.End),
            ], rows));
    }
}

/// <summary>
/// Deductions and contributions: per pay code, what people had taken and what the employer added.
/// </summary>
public sealed class DeductionsAndContributionsReport : PayrollReportBase, IReport
{
    public DeductionsAndContributionsReport(PayrollReportReader reader, ILocalClock localClock, IStringLocalizer<DeductionsAndContributionsReport> stringLocalizer)
        : base(reader, localClock, stringLocalizer)
    {
    }

    public static string Name => "payroll-deductions-contributions";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<DeductionsAndContributionsReport>("Deductions and Contributions");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<DeductionsAndContributionsReport>("Per pay code, the deductions taken from people and the employer's contributions in the period, to pay to plans and agencies.");

    public static string Category => ReportsConstants.Categories.WorkforcePayroll;

    public static Permission Permission => PayrollPermissions.ViewPayRuns;

    public static Permission ExportPermission => PayrollPermissions.ExportPayrollData;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (scope, from, to) = await ReadAsync(context, cancellationToken);
        var entries = (await Reader.ListStatementsAsync(scope, from, to, cancellationToken))
            .SelectMany(statement => statement.Lines.Where(line => line.Type is PayLineType.Deduction or PayLineType.EmployerContribution).Select(line => (statement.Currency, Line: line)))
            .ToList();
        var title = S["Deductions and contributions"].Value;

        if (entries.Count == 0)
        {
            return Empty(context, title, S["No deduction or contribution in these days."].Value);
        }

        var rows = entries
            .GroupBy(entry => (Name: (entry.Line.Description ?? entry.Line.PayCodeId ?? string.Empty).Replace(": employer match", string.Empty, StringComparison.Ordinal), entry.Line.PayCodeId, entry.Currency))
            .OrderBy(group => group.Key.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new ReportRow(
            [
                group.Key.Name,
                Money(group.Where(entry => entry.Line.Type == PayLineType.Deduction).Sum(entry => entry.Line.Amount), group.Key.Currency),
                Money(group.Where(entry => entry.Line.Type == PayLineType.EmployerContribution).Sum(entry => entry.Line.Amount), group.Key.Currency),
                Money(group.Sum(entry => entry.Line.Amount), group.Key.Currency),
            ]))
            .ToList();

        return new ReportDocument()
            .Add(ReportSection.ForTable(title, [new(S["Pay code"].Value), new(S["Taken from people"].Value, ReportColumnAlign.End), new(S["Employer"].Value, ReportColumnAlign.End), new(S["Total to pay"].Value, ReportColumnAlign.End)], rows));
    }
}

/// <summary>
/// Labor distribution: gross pay by the department and work site it is charged to.
/// </summary>
public sealed class LaborDistributionReport : PayrollReportBase, IReport
{
    private readonly CrestApps.OrchardCore.Teams.Services.ITeamManager _teams;

    public LaborDistributionReport(PayrollReportReader reader, ILocalClock localClock, CrestApps.OrchardCore.Teams.Services.ITeamManager teams, IEnumerable<CrestApps.OrchardCore.Payroll.Core.Services.PayrollTeamsFeature> teamsFeature, IStringLocalizer<LaborDistributionReport> stringLocalizer)
        : base(reader, localClock, stringLocalizer)
    {
        // Without Teams on, the teams' tables don't exist: lines show their team ids.
        _teams = teamsFeature.Any() ? teams : null;
    }

    public static string Name => "payroll-labor-distribution";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LaborDistributionReport>("Labor Distribution");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<LaborDistributionReport>("Gross pay and hours of the period by the department and work site each line is charged to.");

    public static string Category => ReportsConstants.Categories.WorkforcePayroll;

    public static Permission Permission => PayrollPermissions.ViewPayRuns;

    public static Permission ExportPermission => PayrollPermissions.ExportPayrollData;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (scope, from, to) = await ReadAsync(context, cancellationToken);
        var entries = (await Reader.ListStatementsAsync(scope, from, to, cancellationToken))
            .SelectMany(statement => statement.Lines.Where(line => line.Type == PayLineType.Earning).Select(line => (statement.Currency, Line: line)))
            .ToList();
        var title = S["Labor distribution"].Value;

        if (entries.Count == 0)
        {
            return Empty(context, title, S["No earning in these days."].Value);
        }

        var teamIds = entries.SelectMany(entry => new[] { entry.Line.Allocation?.DepartmentTeamId, entry.Line.Allocation?.SiteTeamId }).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var teamId in teamIds)
        {
            names[teamId] = (_teams is null ? null : (await _teams.FindByIdAsync(teamId, cancellationToken))?.Name) ?? teamId;
        }

        string NameOf(string id)
            => string.IsNullOrEmpty(id) ? S["(none)"].Value : names.GetValueOrDefault(id) ?? id;

        var rows = entries
            .GroupBy(entry => (Department: NameOf(entry.Line.Allocation?.DepartmentTeamId), Site: NameOf(entry.Line.Allocation?.SiteTeamId), entry.Currency))
            .OrderBy(group => group.Key.Department, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(group => group.Key.Site, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new ReportRow(
            [
                group.Key.Department,
                group.Key.Site,
                group.Sum(entry => entry.Line.Hours ?? 0).ToString("0.##", CultureInfo.CurrentCulture),
                Money(group.Sum(entry => entry.Line.Amount), group.Key.Currency),
            ]))
            .ToList();

        return new ReportDocument()
            .Add(ReportSection.ForTable(title, [new(S["Department"].Value), new(S["Work site"].Value), new(S["Hours"].Value, ReportColumnAlign.End), new(S["Gross"].Value, ReportColumnAlign.End)], rows));
    }
}

/// <summary>
/// Year-to-date earnings: per person and employer, the year's gross, taxes, deductions and net pay up to the end of the
/// range.
/// </summary>
public sealed class YearToDateEarningsReport : PayrollReportBase, IReport
{
    public YearToDateEarningsReport(PayrollReportReader reader, ILocalClock localClock, IStringLocalizer<YearToDateEarningsReport> stringLocalizer)
        : base(reader, localClock, stringLocalizer)
    {
    }

    public static string Name => "payroll-year-to-date-earnings";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<YearToDateEarningsReport>("Year-to-date Earnings");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<YearToDateEarningsReport>("Per person and employer, the year's gross pay, taxes, deductions and net pay up to the end of the period.");

    public static string Category => ReportsConstants.Categories.WorkforcePayroll;

    public static Permission Permission => PayrollPermissions.ViewPayRuns;

    public static Permission ExportPermission => PayrollPermissions.ExportPayrollData;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.Today;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (scope, _, to) = await ReadAsync(context, cancellationToken);
        var end = to ?? DateOnly.FromDateTime((await LocalClock.GetLocalNowAsync()).DateTime);
        var employers = await Reader.EmployerNamesAsync(cancellationToken);
        var statements = await Reader.ListStatementsAsync(scope, new DateOnly(end.Year, 1, 1), end, cancellationToken);
        var title = S["Year-to-date earnings, {0}", end.Year].Value;

        if (statements.Count == 0)
        {
            return Empty(context, title, S["No pay statement was paid this year."].Value);
        }

        var rows = statements
            .GroupBy(statement => (statement.StaffMemberId, statement.EmployerId, statement.Currency))
            .Select(group => (group.Key, Name: group.Last().Snapshot?.EmployeeName ?? group.Key.StaffMemberId, Statements: group.ToList()))
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(entry => new ReportRow(
            [
                entry.Name,
                employers.GetValueOrDefault(entry.Key.EmployerId) ?? entry.Key.EmployerId,
                Money(entry.Statements.Sum(statement => statement.Gross), entry.Key.Currency),
                Money(entry.Statements.Sum(statement => Sum(statement, IsEmployeeTax)), entry.Key.Currency),
                Money(entry.Statements.Sum(statement => Sum(statement, line => line.Type == PayLineType.Deduction)), entry.Key.Currency),
                Money(entry.Statements.Sum(statement => statement.Net), entry.Key.Currency),
            ]))
            .ToList();

        return new ReportDocument()
            .Add(ReportSection.ForTable(title,
            [
                new(S["Person"].Value), new(S["Employer"].Value), new(S["Gross"].Value, ReportColumnAlign.End),
                new(S["Taxes"].Value, ReportColumnAlign.End), new(S["Deductions"].Value, ReportColumnAlign.End), new(S["Net pay"].Value, ReportColumnAlign.End),
            ], rows));
    }
}
