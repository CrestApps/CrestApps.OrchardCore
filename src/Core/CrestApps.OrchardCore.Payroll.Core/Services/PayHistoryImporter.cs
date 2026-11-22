using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// The default <see cref="IPayHistoryImporter"/>: it writes statements and historical runs to their stores directly, so
/// no pay run or statement observer hears of them, and recomputes the totals of each run it touched.
/// </summary>
public sealed class PayHistoryImporter : IPayHistoryImporter
{
    private const string DefaultImportedBy = "Import";

    private readonly PayStatementStore _statements;
    private readonly IPayRunManager _runs;
    private readonly EmployerStore _employers;
    private readonly IStaffMemberManager _staff;
    private readonly INumberSequence _numbers;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayHistoryImporter"/> class.
    /// </summary>
    /// <param name="statements">The pay statements.</param>
    /// <param name="runs">The pay runs.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="staff">The staff members.</param>
    /// <param name="numbers">The number sequences, for a statement with no number.</param>
    /// <param name="clock">The clock.</param>
    public PayHistoryImporter(
        PayStatementStore statements,
        IPayRunManager runs,
        EmployerStore employers,
        IStaffMemberManager staff,
        INumberSequence numbers,
        IClock clock)
    {
        _statements = statements;
        _runs = runs;
        _employers = employers;
        _staff = staff;
        _numbers = numbers;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<PayHistoryImportResult> ImportAsync(IEnumerable<PayHistoryEntry> entries, string importedBy = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var result = new PayHistoryImportResult();
        var runs = new Dictionary<(string EmployerId, DateOnly PayDate), PayRun>();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var statement = entry?.Statement ?? new PayStatement();

            if (string.IsNullOrEmpty(statement.StaffMemberId) && !string.IsNullOrEmpty(entry?.EmployeeNumber))
            {
                statement.StaffMemberId = (await _staff.FindByEmployeeNumberAsync(entry.EmployeeNumber, cancellationToken))?.ItemId;
            }

            if (string.IsNullOrEmpty(statement.EmployerId) || await _employers.FindByIdAsync(statement.EmployerId, cancellationToken) is null)
            {
                result.Errors.Add("A pay statement names no employer of the site.");

                continue;
            }

            if (string.IsNullOrEmpty(statement.StaffMemberId) || await _staff.FindByIdAsync(statement.StaffMemberId, cancellationToken) is null)
            {
                result.Errors.Add("A pay statement names no staff member of the site (StaffMemberId or EmployeeNumber).");

                continue;
            }

            if (statement.PayDate == default || statement.Status == PayStatementStatus.Draft)
            {
                result.Errors.Add("A pay statement has no pay date or is a draft: only final statements are history.");

                continue;
            }

            // A tax line without its taxable wages would leave the year's Social Security and Medicare wages short.
            if (statement.Lines.Any(line => line.Type == PayLineType.Tax && line.TaxableWages is null))
            {
                result.Errors.Add("A pay statement has a tax line without TaxableWages: give the wages each tax was worked out on.");

                continue;
            }

            // Already there: the import can run again.
            if (!string.IsNullOrEmpty(statement.ItemId) && await _statements.FindByIdAsync(statement.ItemId, cancellationToken) is not null)
            {
                result.Skipped++;

                continue;
            }

            var employerId = statement.EmployerId;
            var statementNumber = statement.Number;

            if (!string.IsNullOrEmpty(statementNumber) && (await _statements.ListAsync(index => index.EmployerId == employerId && index.Number == statementNumber, cancellationToken)).Count > 0)
            {
                result.Skipped++;

                continue;
            }

            var run = await HistoricalRunAsync(runs, statement, string.IsNullOrWhiteSpace(importedBy) ? DefaultImportedBy : importedBy, cancellationToken);

            statement.ItemId = string.IsNullOrEmpty(statement.ItemId) ? UniqueId.GenerateId() : statement.ItemId;
            statement.RunId = run.ItemId;
            statement.Number = string.IsNullOrEmpty(statement.Number)
                ? (await _numbers.NextAsync(PayrollConstants.PayStatementNumberSeries, cancellationToken: cancellationToken)).ToString(CultureInfo.InvariantCulture)
                : statement.Number;
            statement.Status = statement.Status == PayStatementStatus.Final ? PayStatementStatus.Paid : statement.Status;
            statement.CreatedUtc = statement.CreatedUtc == default ? _clock.UtcNow : statement.CreatedUtc;
            statement.ProcessedUtc ??= statement.CreatedUtc;

            // References to disbursements and checks of the other system mean nothing here.
            foreach (var payment in statement.Payments)
            {
                payment.ReferenceId = null;
            }

            await _statements.CreateAsync(statement, cancellationToken);
            result.Imported.Add(statement);

            if (!run.StaffMemberIds.Contains(statement.StaffMemberId))
            {
                run.StaffMemberIds.Add(statement.StaffMemberId);
            }
        }

        foreach (var run in runs.Values)
        {
            var statements = await _statements.ListAsync(index => index.RunId == run.ItemId, cancellationToken);

            run.Totals = DefaultPayRunService.Totals(statements.Concat(result.Imported.Where(statement => statement.RunId == run.ItemId)).DistinctBy(statement => statement.ItemId));
            await _runs.UpdateAsync(run, cancellationToken: cancellationToken);
        }

        return result;
    }

    // The historical run of an employer and a pay date: found, or added paid.
    private async Task<PayRun> HistoricalRunAsync(Dictionary<(string EmployerId, DateOnly PayDate), PayRun> runs, PayStatement statement, string importedBy, CancellationToken cancellationToken)
    {
        var key = (statement.EmployerId, statement.PayDate);

        if (runs.TryGetValue(key, out var run))
        {
            return run;
        }

        run = (await _runs.ListByEmployersAsync([statement.EmployerId], cancellationToken)).FirstOrDefault(candidate => candidate.Type == PayRunType.Historical && candidate.PayDate == statement.PayDate);

        if (run is null)
        {
            run = await _runs.NewAsync(cancellationToken: cancellationToken);
            run.EmployerId = statement.EmployerId;
            run.Type = PayRunType.Historical;
            run.PeriodStart = statement.PeriodStart == default ? statement.PayDate : statement.PeriodStart;
            run.PeriodEnd = statement.PeriodEnd == default ? statement.PayDate : statement.PeriodEnd;
            run.PayDate = statement.PayDate;
            run.StaffMemberIds = [statement.StaffMemberId];
            run.Status = PayRunStatus.Paid;
            run.ProcessedUtc = _clock.UtcNow;
            run.ProcessedBy = importedBy;
            await _runs.CreateAsync(run, cancellationToken);
        }

        runs[key] = run;

        return run;
    }
}
