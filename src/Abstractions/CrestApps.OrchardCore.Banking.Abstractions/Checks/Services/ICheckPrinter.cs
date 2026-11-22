using CrestApps.OrchardCore.Banking.Checks.Models;

namespace CrestApps.OrchardCore.Banking.Checks.Services;

/// <summary>
/// Prints checks to one PDF with a layout: each check with its voucher, the signature and the check line. A run on
/// pre-printed stock prints the checks of one bank account only, since the stock carries the account. Checks written by
/// hand are never printed.
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

    /// <summary>
    /// Prints queued checks (<see cref="CheckStatus.Queued"/>) of one bank account on pre-printed stock, numbered from
    /// the number of the first sheet: the first check gets <paramref name="firstNumber"/>, the next one more, and so on.
    /// The numbers are printed, not saved; record the run with <see cref="ICheckService.ConfirmPrintRunAsync"/> once the
    /// person says which sheets misprinted.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="checks">The queued checks, in the order they print.</param>
    /// <param name="printRunId">The print run.</param>
    /// <param name="firstNumber">The number printed on the first sheet.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The PDF, or why the checks could not print.</returns>
    Task<CheckPrintResult> PrintAsync(CheckLayout layout, IReadOnlyList<CheckRecord> checks, string printRunId, long firstNumber, CancellationToken cancellationToken = default);
}
