using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Imports pay statements paid elsewhere (another payroll system, an older app) as history, so the year's totals,
/// wage bases and year-end forms start right. Each statement lands, final and paid, in a historical pay run of its
/// employer and pay date, which can't be calculated, processed or voided; pay run and statement observers are not told,
/// since nothing is being paid. A statement already there (same id, or same employer and number) is skipped, so an
/// import can run again. The PayrollPayHistory recipe step uses it; an app calls it from code, such as a background job.
/// </summary>
public interface IPayHistoryImporter
{
    /// <summary>
    /// Imports statements as history. Changes are saved with the rest of the unit of work (the request's or the job's
    /// session).
    /// </summary>
    /// <param name="entries">The statements, each with the employee number to find its staff member by when it names none.</param>
    /// <param name="importedBy">Who or what imported them, kept on the historical runs it adds; or <see langword="null"/> for "Import".</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was imported, skipped and refused.</returns>
    Task<PayHistoryImportResult> ImportAsync(IEnumerable<PayHistoryEntry> entries, string importedBy = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// A pay statement to import as history.
/// </summary>
public sealed class PayHistoryEntry
{
    /// <summary>
    /// Gets or sets the statement: its employer, staff member, pay date and period, lines (each tax line with the wages
    /// it was worked out on) and payments. Its status is Final, Paid or Voided; Final is imported as Paid.
    /// </summary>
    public PayStatement Statement { get; set; }

    /// <summary>
    /// Gets or sets the staff member's employee number, used when the statement names no staff member.
    /// </summary>
    public string EmployeeNumber { get; set; }
}

/// <summary>
/// What an import of pay history did.
/// </summary>
public sealed class PayHistoryImportResult
{
    /// <summary>
    /// Gets the statements imported.
    /// </summary>
    public IList<PayStatement> Imported { get; } = [];

    /// <summary>
    /// Gets or sets how many statements were already there and skipped.
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Gets why statements were refused, one message each.
    /// </summary>
    public IList<string> Errors { get; } = [];
}
