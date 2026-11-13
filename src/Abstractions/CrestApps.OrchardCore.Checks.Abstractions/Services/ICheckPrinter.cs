using CrestApps.OrchardCore.Checks.Models;

namespace CrestApps.OrchardCore.Checks.Services;

/// <summary>
/// Prints checks to one PDF with a layout: each check with its voucher, the signature and the check line.
/// </summary>
public interface ICheckPrinter
{
    /// <summary>
    /// Prints checks. It does not change their status; record the print with <see cref="ICheckService.MarkPrintedAsync"/>.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="checks">The checks, in the order they print.</param>
    /// <param name="printRunId">The print run, named in the Audit Trail entry of each bank account number read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The PDF, or why the checks could not print.</returns>
    Task<CheckPrintResult> PrintAsync(CheckLayout layout, IReadOnlyList<CheckRecord> checks, string printRunId, CancellationToken cancellationToken = default);
}
