using CrestApps.OrchardCore.Checks.Models;

namespace CrestApps.OrchardCore.Checks.Services;

/// <summary>
/// The rules of checks: numbering per bank account, the status table, voids, reissues and who is told. It checks no
/// permission; the caller does.
/// </summary>
public interface ICheckService
{
    /// <summary>
    /// Issues a check with the next number of its bank account.
    /// </summary>
    /// <param name="request">What the check pays.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The check, or why it was refused.</returns>
    Task<CheckResult> IssueAsync(CheckIssueRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number the next check of a bank account gets.
    /// </summary>
    /// <param name="bankAccountId">The bank account.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The next number.</returns>
    Task<long> GetNextNumberAsync(string bankAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the number the next check of a bank account gets, such as the first number of newly loaded stock. A number
    /// already used by a check of the account is refused.
    /// </summary>
    /// <param name="bankAccountId">The bank account.</param>
    /// <param name="next">The next number, 1 or more.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when a check of the account already has that number or a later one.</returns>
    Task<bool> SetNextNumberAsync(string bankAccountId, long next, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that checks were printed in one run.
    /// </summary>
    /// <param name="checks">The checks.</param>
    /// <param name="printRunId">The print run.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the checks are saved.</returns>
    Task MarkPrintedAsync(IEnumerable<CheckRecord> checks, string printRunId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids a check. It keeps its number.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the check can't be voided.</returns>
    Task<bool> VoidAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a check with a new one with the next number, for the same payee, amount and source, such as for a
    /// spoiled sheet or a lost check. The old check becomes <see cref="CheckStatus.Reissued"/> and links the new one.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The new check, or why it was refused.</returns>
    Task<CheckResult> ReissueAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the bank was asked not to pay a check.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the move is not allowed.</returns>
    Task<bool> StopPaymentAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the bank paid a check.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="clearedOn">The date it was paid.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the move is not allowed.</returns>
    Task<bool> MarkClearedAsync(CheckRecord check, DateOnly clearedOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a check became too old for the bank to pay.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the move is not allowed.</returns>
    Task<bool> MarkStaleAsync(CheckRecord check, CancellationToken cancellationToken = default);
}
