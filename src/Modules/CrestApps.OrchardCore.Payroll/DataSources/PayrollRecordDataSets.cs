using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Payroll.DataSources;

/// <summary>
/// The base of Payroll's data sets of runs and statements: read by those who may see the runs of an employer
/// (<see cref="PayrollPermissions.ViewPayRuns"/>, globally or per employer), each reading only the employers they may see,
/// filtered by pay date.
/// </summary>
/// <typeparam name="TRow">The row.</typeparam>
public abstract class PayrollRecordDataSet<TRow> : RecordDataSet<TRow>, IPayrollDataSet
{
    protected PayrollRecordDataSet(DataSetDescriptor descriptor, PayrollReportReader reader)
        : base(descriptor)
    {
        Reader = reader;
    }

    protected PayrollReportReader Reader { get; }

    protected virtual Permission Permission => PayrollPermissions.ViewPayRuns;

    /// <inheritdoc />
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => !(await Reader.GetScopeAsync(context?.User, Permission, cancellationToken)).IsEmpty;

    // The scope and the pay dates of a query.
    protected async Task<(PayrollScope Scope, DateOnly? From, DateOnly? To)> ReadQueryAsync(DataSourceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (from, to) = DataConditions.DateRange(query.Conditions ?? [], "PayDate");

        return (
            await Reader.GetScopeAsync(query.Context?.User, Permission, cancellationToken),
            from.HasValue ? DateOnly.FromDateTime(from.Value) : null,
            to.HasValue ? DateOnly.FromDateTime(to.Value) : null);
    }

    protected static DateTime Day(DateOnly day)
        => day.ToDateTime(TimeOnly.MinValue);
}

/// <summary>A pay run with its employer's name.</summary>
public sealed record PayRunRow(PayRun Run, string EmployerName);

/// <summary>A statement with its employer's name.</summary>
public sealed record PayStatementRow(PayStatement Statement, string EmployerName);

/// <summary>A line of a statement.</summary>
public sealed record PayStatementLineRow(PayStatement Statement, PayStatementLine Line, string EmployerName);

/// <summary>A payment of a statement.</summary>
public sealed record PayStatementPaymentRow(PayStatement Statement, PayStatementPayment Payment, string EmployerName);

/// <summary>The year's total of one line of one person with one employer.</summary>
public sealed record YearToDateRow(string StaffMemberId, string EmployeeName, string EmployerId, string EmployerName, int Year, string Line, string Type, string Currency, decimal Amount, decimal Hours, decimal TaxableWages);

/// <summary>
/// Pay runs: one row per run with its totals.
/// </summary>
public sealed class PayRunsDataSet : PayrollRecordDataSet<PayRunRow>
{
    public PayRunsDataSet(PayrollReportReader reader, IStringLocalizer<PayRunsDataSet> stringLocalizer)
        : base(new DataSetDescriptor("PayRuns", stringLocalizer["Pay runs"], stringLocalizer["One row per pay run: employer, type, period, pay date, status and its totals, with the cash the employer needs."]), reader)
    {
        var S = stringLocalizer;
        var run = S["Run"].Value;
        var totals = S["Totals"].Value;

        AddField("PayRunId", S["Pay run ID"], DataFieldType.Text, row => row.Run.ItemId, run).Identifier();
        AddField("Number", S["Number"], DataFieldType.Text, row => row.Run.Number, run);
        AddField("EmployerId", S["Employer ID"], DataFieldType.Text, row => row.Run.EmployerId, run);
        AddField("EmployerName", S["Employer"], DataFieldType.Text, row => row.EmployerName, run);
        AddField("Type", S["Type"], DataFieldType.Text, row => row.Run.Type.ToString(), run);
        AddField("Status", S["Status"], DataFieldType.Text, row => row.Run.Status.ToString(), run);
        AddField("PeriodStart", S["Period start"], DataFieldType.Date, row => Day(row.Run.PeriodStart), run);
        AddField("PeriodEnd", S["Period end"], DataFieldType.Date, row => Day(row.Run.PeriodEnd), run);
        AddField("PayDate", S["Pay date"], DataFieldType.Date, row => Day(row.Run.PayDate), run);
        AddField("PreparedBy", S["Prepared by"], DataFieldType.Text, row => row.Run.PreparedBy, run);
        AddField("ProcessedBy", S["Processed by"], DataFieldType.Text, row => row.Run.ProcessedBy, run);
        AddField("Statements", S["Statements"], DataFieldType.Integer, row => row.Run.Totals.Statements, totals);
        AddField("Gross", S["Gross"], DataFieldType.Decimal, row => row.Run.Totals.Gross, totals);
        AddField("EmployeeTaxes", S["Employees' taxes"], DataFieldType.Decimal, row => row.Run.Totals.EmployeeTaxes, totals);
        AddField("EmployerTaxes", S["Employer's taxes"], DataFieldType.Decimal, row => row.Run.Totals.EmployerTaxes, totals);
        AddField("Deductions", S["Deductions"], DataFieldType.Decimal, row => row.Run.Totals.Deductions, totals);
        AddField("EmployerContributions", S["Employer contributions"], DataFieldType.Decimal, row => row.Run.Totals.EmployerContributions, totals);
        AddField("Reimbursements", S["Reimbursements"], DataFieldType.Decimal, row => row.Run.Totals.Reimbursements, totals);
        AddField("Net", S["Net pay"], DataFieldType.Decimal, row => row.Run.Totals.Net, totals);
        AddField("CashNeeded", S["Cash needed"], DataFieldType.Decimal, row => row.Run.Totals.CashNeeded, totals);

        Descriptor.DefaultDateField = "PayDate";
    }

    protected override async IAsyncEnumerable<PayRunRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (scope, from, to) = await ReadQueryAsync(query, cancellationToken);
        var employers = await Reader.EmployerNamesAsync(cancellationToken);

        foreach (var run in (await Reader.ListRunsAsync(scope, from, to, cancellationToken)).OrderBy(run => run.PayDate))
        {
            yield return new PayRunRow(run, employers.GetValueOrDefault(run.EmployerId));
        }
    }
}

/// <summary>
/// Pay statements: one row per final statement (reversals included, with negative amounts).
/// </summary>
public sealed class PayStatementsDataSet : PayrollRecordDataSet<PayStatementRow>
{
    public PayStatementsDataSet(PayrollReportReader reader, IStringLocalizer<PayStatementsDataSet> stringLocalizer)
        : base(new DataSetDescriptor("PayStatements", stringLocalizer["Pay statements"], stringLocalizer["One row per final pay statement: who, the employer, the period and pay date, gross and net pay, and its status. A void adds a reversing statement with the opposite amounts."]), reader)
    {
        var S = stringLocalizer;
        var statement = S["Statement"].Value;
        var person = S["Staff member"].Value;

        AddField("StatementId", S["Statement ID"], DataFieldType.Text, row => row.Statement.ItemId, statement).Identifier();
        AddField("Number", S["Number"], DataFieldType.Text, row => row.Statement.Number, statement);
        AddField("PayRunId", S["Pay run ID"], DataFieldType.Text, row => row.Statement.RunId, statement);
        AddField("EmployerId", S["Employer ID"], DataFieldType.Text, row => row.Statement.EmployerId, statement);
        AddField("EmployerName", S["Employer"], DataFieldType.Text, row => row.EmployerName, statement);
        AddField("StaffMemberId", S["Staff member ID"], DataFieldType.Text, row => row.Statement.StaffMemberId, person);
        AddField("EmployeeName", S["Name"], DataFieldType.Text, row => row.Statement.Snapshot?.EmployeeName, person);
        AddField("EmployeeNumber", S["Employee number"], DataFieldType.Text, row => row.Statement.Snapshot?.EmployeeNumber, person);
        AddField("PeriodStart", S["Period start"], DataFieldType.Date, row => Day(row.Statement.PeriodStart), statement);
        AddField("PeriodEnd", S["Period end"], DataFieldType.Date, row => Day(row.Statement.PeriodEnd), statement);
        AddField("PayDate", S["Pay date"], DataFieldType.Date, row => Day(row.Statement.PayDate), statement);
        AddField("Status", S["Status"], DataFieldType.Text, row => row.Statement.VoidOf is null ? row.Statement.Status.ToString() : "Reversal", statement);
        AddField("ReversesId", S["Reverses statement ID"], DataFieldType.Text, row => row.Statement.VoidOf, statement);
        AddField("Currency", S["Currency"], DataFieldType.Text, row => row.Statement.Currency, statement);
        AddField("Gross", S["Gross"], DataFieldType.Decimal, row => row.Statement.Gross, statement);
        AddField("Net", S["Net pay"], DataFieldType.Decimal, row => row.Statement.Net, statement);

        Descriptor.DefaultDateField = "PayDate";
    }

    protected override async IAsyncEnumerable<PayStatementRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (scope, from, to) = await ReadQueryAsync(query, cancellationToken);
        var employers = await Reader.EmployerNamesAsync(cancellationToken);

        foreach (var statement in (await Reader.ListStatementsAsync(scope, from, to, cancellationToken)).OrderBy(statement => statement.PayDate).ThenBy(statement => statement.Number, StringComparer.Ordinal))
        {
            yield return new PayStatementRow(statement, employers.GetValueOrDefault(statement.EmployerId));
        }
    }
}

/// <summary>
/// The base of the views of statement lines by class: earnings, deductions, taxes and employer contributions.
/// </summary>
public abstract class PayStatementLinesDataSet : PayrollRecordDataSet<PayStatementLineRow>
{
    private readonly PayLineType[] _types;

    protected PayStatementLinesDataSet(string name, LocalizedString displayName, LocalizedString description, PayrollReportReader reader, IStringLocalizer S, params PayLineType[] types)
        : base(new DataSetDescriptor(name, displayName, description), reader)
    {
        _types = types;

        var statement = S["Statement"].Value;
        var line = S["Line"].Value;
        var cost = S["Cost"].Value;

        AddField("StatementId", S["Statement ID"], DataFieldType.Text, row => row.Statement.ItemId, statement);
        AddField("Number", S["Statement number"], DataFieldType.Text, row => row.Statement.Number, statement);
        AddField("EmployerId", S["Employer ID"], DataFieldType.Text, row => row.Statement.EmployerId, statement);
        AddField("EmployerName", S["Employer"], DataFieldType.Text, row => row.EmployerName, statement);
        AddField("StaffMemberId", S["Staff member ID"], DataFieldType.Text, row => row.Statement.StaffMemberId, statement);
        AddField("EmployeeName", S["Name"], DataFieldType.Text, row => row.Statement.Snapshot?.EmployeeName, statement);
        AddField("PayDate", S["Pay date"], DataFieldType.Date, row => Day(row.Statement.PayDate), statement);
        AddField("Type", S["Type"], DataFieldType.Text, row => row.Line.Type.ToString(), line);
        AddField("PayCodeId", S["Pay code ID"], DataFieldType.Text, row => row.Line.PayCodeId, line);
        AddField("TaxCode", S["Tax code"], DataFieldType.Text, row => row.Line.TaxCode, line);
        AddField("TaxCategory", S["Tax category"], DataFieldType.Text, row => row.Line.TaxCategory, line);
        AddField("Description", S["Description"], DataFieldType.Text, row => row.Line.Description, line);
        AddField("Jurisdiction", S["Jurisdiction"], DataFieldType.Text, row => row.Line.Jurisdiction, line);
        AddField("Payer", S["Paid by"], DataFieldType.Text, row => row.Line.Payer.ToString(), line);
        AddField("Hours", S["Hours"], DataFieldType.Decimal, row => row.Line.Hours, line);
        AddField("Rate", S["Rate"], DataFieldType.Decimal, row => row.Line.Rate, line);
        AddField("Amount", S["Amount"], DataFieldType.Decimal, row => row.Line.Amount, line);
        AddField("TaxableWages", S["Taxable wages"], DataFieldType.Decimal, row => row.Line.TaxableWages, line);
        AddField("Currency", S["Currency"], DataFieldType.Text, row => row.Statement.Currency, line);
        AddField("DepartmentTeamId", S["Department team ID"], DataFieldType.Text, row => row.Line.Allocation?.DepartmentTeamId, cost);
        AddField("SiteTeamId", S["Work site team ID"], DataFieldType.Text, row => row.Line.Allocation?.SiteTeamId, cost);

        Descriptor.DefaultDateField = "PayDate";
    }

    protected override async IAsyncEnumerable<PayStatementLineRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (scope, from, to) = await ReadQueryAsync(query, cancellationToken);
        var employers = await Reader.EmployerNamesAsync(cancellationToken);

        foreach (var statement in (await Reader.ListStatementsAsync(scope, from, to, cancellationToken)).OrderBy(statement => statement.PayDate))
        {
            foreach (var line in statement.Lines.Where(line => _types.Contains(line.Type)))
            {
                yield return new PayStatementLineRow(statement, line, employers.GetValueOrDefault(statement.EmployerId));
            }
        }
    }
}

/// <summary>Earnings: the earning and imputed income lines of final statements.</summary>
public sealed class PayEarningsDataSet : PayStatementLinesDataSet
{
    public PayEarningsDataSet(PayrollReportReader reader, IStringLocalizer<PayEarningsDataSet> S)
        : base("PayEarnings", S["Earnings"], S["One row per earning line of a final statement: the pay code, hours, rate and amount, with the department and work site it is charged to."], reader, S, PayLineType.Earning, PayLineType.ImputedIncome, PayLineType.Reimbursement)
    {
    }
}

/// <summary>Deductions: the deduction lines of final statements.</summary>
public sealed class PayDeductionsDataSet : PayStatementLinesDataSet
{
    public PayDeductionsDataSet(PayrollReportReader reader, IStringLocalizer<PayDeductionsDataSet> S)
        : base("PayDeductions", S["Deductions"], S["One row per deduction taken on a final statement, such as a retirement plan or a loan repayment."], reader, S, PayLineType.Deduction)
    {
    }
}

/// <summary>Taxes: the tax lines of final statements, the employee's and the employer's.</summary>
public sealed class PayTaxesDataSet : PayStatementLinesDataSet
{
    public PayTaxesDataSet(PayrollReportReader reader, IStringLocalizer<PayTaxesDataSet> S)
        : base("PayTaxes", S["Taxes"], S["One row per tax line of a final statement, withheld from the person or paid by the employer, with its jurisdiction and taxable wages."], reader, S, PayLineType.Tax)
    {
    }
}

/// <summary>Employer contributions: what the employer pays on top of pay, such as a retirement match.</summary>
public sealed class PayEmployerContributionsDataSet : PayStatementLinesDataSet
{
    public PayEmployerContributionsDataSet(PayrollReportReader reader, IStringLocalizer<PayEmployerContributionsDataSet> S)
        : base("PayEmployerContributions", S["Employer contributions"], S["One row per employer contribution of a final statement, such as a retirement plan match."], reader, S, PayLineType.EmployerContribution)
    {
    }
}

/// <summary>
/// Payments: how net pay of each final statement was paid. No bank account number is ever read.
/// </summary>
public sealed class PayStatementPaymentsDataSet : PayrollRecordDataSet<PayStatementPaymentRow>
{
    public PayStatementPaymentsDataSet(PayrollReportReader reader, IStringLocalizer<PayStatementPaymentsDataSet> stringLocalizer)
        : base(new DataSetDescriptor("PayPayments", stringLocalizer["Payments of net pay"], stringLocalizer["One row per payment of net pay of a final statement: by deposit or check, the amount, its reference (the check number or the account's name) and whether it was paid."]), reader)
    {
        var S = stringLocalizer;
        var statement = S["Statement"].Value;
        var payment = S["Payment"].Value;

        AddField("StatementId", S["Statement ID"], DataFieldType.Text, row => row.Statement.ItemId, statement);
        AddField("Number", S["Statement number"], DataFieldType.Text, row => row.Statement.Number, statement);
        AddField("EmployerId", S["Employer ID"], DataFieldType.Text, row => row.Statement.EmployerId, statement);
        AddField("EmployerName", S["Employer"], DataFieldType.Text, row => row.EmployerName, statement);
        AddField("StaffMemberId", S["Staff member ID"], DataFieldType.Text, row => row.Statement.StaffMemberId, statement);
        AddField("EmployeeName", S["Name"], DataFieldType.Text, row => row.Statement.Snapshot?.EmployeeName, statement);
        AddField("PayDate", S["Pay date"], DataFieldType.Date, row => Day(row.Statement.PayDate), statement);
        AddField("Method", S["Method"], DataFieldType.Text, row => row.Payment.Method.ToString(), payment);
        AddField("Amount", S["Amount"], DataFieldType.Decimal, row => row.Payment.Amount, payment);
        AddField("Currency", S["Currency"], DataFieldType.Text, row => row.Statement.Currency, payment);
        AddField("Reference", S["Reference"], DataFieldType.Text, row => row.Payment.Reference, payment);
        AddField("PaymentId", S["Disbursement or check ID"], DataFieldType.Text, row => row.Payment.ReferenceId, payment);
        AddField("Paid", S["Paid"], DataFieldType.Boolean, row => !string.IsNullOrEmpty(row.Payment.ReferenceId), payment);
        AddField("Problem", S["Problem"], DataFieldType.Text, row => row.Payment.Problem, payment);
        AddField("Attempt", S["Reissues"], DataFieldType.Integer, row => row.Payment.Attempt, payment);

        Descriptor.DefaultDateField = "PayDate";
    }

    protected override async IAsyncEnumerable<PayStatementPaymentRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (scope, from, to) = await ReadQueryAsync(query, cancellationToken);
        var employers = await Reader.EmployerNamesAsync(cancellationToken);

        foreach (var statement in (await Reader.ListStatementsAsync(scope, from, to, cancellationToken)).Where(statement => statement.VoidOf is null).OrderBy(statement => statement.PayDate))
        {
            foreach (var payment in statement.Payments)
            {
                yield return new PayStatementPaymentRow(statement, payment, employers.GetValueOrDefault(statement.EmployerId));
            }
        }
    }
}

/// <summary>
/// Year-to-date balances: per person, employer, year and line, the year's total of final statements (voids cancel out).
/// </summary>
public sealed class YearToDateBalancesDataSet : PayrollRecordDataSet<YearToDateRow>
{
    public YearToDateBalancesDataSet(PayrollReportReader reader, IStringLocalizer<YearToDateBalancesDataSet> stringLocalizer)
        : base(new DataSetDescriptor("PayYearToDate", stringLocalizer["Year-to-date balances"], stringLocalizer["Per person, employer and year, the total of each pay code and tax of the final statements paid in the year: amount, hours and taxable wages. A void and its reversal cancel out."]), reader)
    {
        var S = stringLocalizer;
        var who = S["Staff member"].Value;
        var balance = S["Balance"].Value;

        AddField("StaffMemberId", S["Staff member ID"], DataFieldType.Text, row => row.StaffMemberId, who);
        AddField("EmployeeName", S["Name"], DataFieldType.Text, row => row.EmployeeName, who);
        AddField("EmployerId", S["Employer ID"], DataFieldType.Text, row => row.EmployerId, who);
        AddField("EmployerName", S["Employer"], DataFieldType.Text, row => row.EmployerName, who);
        AddField("Year", S["Year"], DataFieldType.Integer, row => row.Year, balance);
        AddField("Line", S["Pay code or tax"], DataFieldType.Text, row => row.Line, balance);
        AddField("Type", S["Type"], DataFieldType.Text, row => row.Type, balance);
        AddField("Amount", S["Amount"], DataFieldType.Decimal, row => row.Amount, balance);
        AddField("Hours", S["Hours"], DataFieldType.Decimal, row => row.Hours, balance);
        AddField("TaxableWages", S["Taxable wages"], DataFieldType.Decimal, row => row.TaxableWages, balance);
        AddField("Currency", S["Currency"], DataFieldType.Text, row => row.Currency, balance);
    }

    protected override async IAsyncEnumerable<YearToDateRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (scope, from, to) = await ReadQueryAsync(query, cancellationToken);
        var employers = await Reader.EmployerNamesAsync(cancellationToken);
        var statements = await Reader.ListStatementsAsync(scope, from, to, cancellationToken);

        var groups = statements
            .SelectMany(statement => statement.Lines.Select(line => (Statement: statement, Line: line)))
            .GroupBy(entry => (entry.Statement.StaffMemberId, entry.Statement.EmployerId, entry.Statement.PayDate.Year, Key: entry.Line.PayCodeId is null ? entry.Line.TaxCode : entry.Line.Description ?? entry.Line.PayCodeId, Type: entry.Line.Type))
            .OrderBy(group => group.Key.StaffMemberId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var first = group.First().Statement;

            yield return new YearToDateRow(
                group.Key.StaffMemberId,
                first.Snapshot?.EmployeeName,
                group.Key.EmployerId,
                employers.GetValueOrDefault(group.Key.EmployerId),
                group.Key.Year,
                group.Key.Key,
                group.Key.Type.ToString(),
                first.Currency,
                group.Sum(entry => entry.Line.Amount),
                group.Sum(entry => entry.Line.Hours ?? 0),
                group.Sum(entry => entry.Line.TaxableWages ?? 0));
        }
    }
}
