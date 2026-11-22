using CrestApps.OrchardCore.Banking.Checks.Models;

namespace CrestApps.OrchardCore.Banking.Checks.Services;

/// <summary>
/// The rules of checks: numbering per bank account, the status table, print runs, voids, reissues and who is told. It
/// asks the <see cref="ICheckChangeHandler"/>s before a void, reissue, clear or un-clear is saved, and tells the
/// <see cref="ICheckObserver"/>s after every change. It checks no permission; the caller does.
/// </summary>
public interface ICheckService
{
    /// <summary>
    /// Issues a check on one of the business's checking or savings accounts, numbered as the request's
    /// <see cref="CheckIssueRequest.Numbering"/> says: the next number of its bank account (skipping a number a check
    /// written by hand already took), no number until a print run on pre-printed stock numbers it
    /// (<see cref="CheckStatus.Queued"/>), or the typed number of a check written by hand, recorded as
    /// <see cref="CheckStatus.Printed"/>. The account needs no routing or account number for this.
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
    /// Checks that queued checks can be printed together on pre-printed stock from a first number: all queued and not
    /// history, all drawn on one bank account, and none of the numbers the run would use taken by a check of the account.
    /// </summary>
    /// <param name="checks">The checks, in the order they print.</param>
    /// <param name="firstNumber">The number printed on the first sheet.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Why they can't; empty when they can.</returns>
    Task<IReadOnlyList<string>> ValidatePrintRunAsync(IReadOnlyList<CheckRecord> checks, long firstNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a print run of queued checks on pre-printed stock once the person said which sheets misprinted: each
    /// check gets the number of its sheet (the first number, then one more each) and becomes
    /// <see cref="CheckStatus.Printed"/>; the number of each misprinted sheet is kept as a new voided check
    /// (<see cref="CheckRecord.IsSpoiled"/>) so it is never used again, and its check stays queued to print again. The
    /// account's number series moves past the last number used. Refused as a whole, with nothing saved, when
    /// <see cref="ValidatePrintRunAsync"/> refuses it.
    /// </summary>
    /// <param name="request">The checks, the first number, the misprints and the run.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The printed checks and the spoiled sheets, or why the run was refused.</returns>
    Task<CheckPrintRunResult> ConfirmPrintRunAsync(CheckPrintRunRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that issued checks were printed in one run. A printed check is printed again only by
    /// <see cref="MarkReprintedAsync"/>. Queued checks are printed only by <see cref="ConfirmPrintRunAsync"/>.
    /// </summary>
    /// <param name="checks">The checks.</param>
    /// <param name="printRunId">The print run.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the checks are saved.</returns>
    Task MarkPrintedAsync(IEnumerable<CheckRecord> checks, string printRunId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a printed check was printed again, such as after a printer jam, with the reason.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="printRunId">The print run.</param>
    /// <param name="reason">Why it was printed again.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the check is not printed, is history or was written by hand.</returns>
    Task<bool> MarkReprintedAsync(CheckRecord check, string printRunId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids a check today in the site's time zone (or on its check date, for a check dated later). It keeps its number.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the check can't be voided or a handler refused it.</returns>
    Task<bool> VoidAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids a check on a date, which is stored as <see cref="CheckRecord.VoidedOn"/> and given to the handlers and the
    /// observers: the module the check pays reverses the payment on that date, and may refuse. It keeps its number. A
    /// date before the check date is refused.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="reason">Why.</param>
    /// <param name="voidedOn">The void date.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The voided check, or why it was refused.</returns>
    Task<CheckResult> VoidAsync(CheckRecord check, string reason, DateOnly voidedOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a check with a new one for the same payee, amount and source, such as for a spoiled sheet or a lost
    /// check: the next number, or no number until it prints for a check that was numbered at print. The old check
    /// becomes <see cref="CheckStatus.Reissued"/> and links the new one. The handlers may refuse it.
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
    /// Records that the bank paid a check. Same as <see cref="ClearAsync"/>, without the reasons of a refusal.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="clearedOn">The date it was paid.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the move is not allowed or a handler refused it.</returns>
    Task<bool> MarkClearedAsync(CheckRecord check, DateOnly clearedOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the bank paid a check on a date, and keeps the status it cleared from for
    /// <see cref="UnclearAsync"/>. The handlers may refuse it.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="clearedOn">The date it was paid.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The cleared check, or why it was refused.</returns>
    Task<CheckResult> ClearAsync(CheckRecord check, DateOnly clearedOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a cleared check was not paid after all, such as a bank line matched to the wrong check: it goes back
    /// to the status it cleared from (<see cref="CheckRecord.ClearedFrom"/>, <see cref="CheckStatus.Printed"/> when not
    /// known), and its cleared date is removed. The handlers may refuse it.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The check, or why it was refused.</returns>
    Task<CheckResult> UnclearAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a check became too old for the bank to pay.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the move is not allowed.</returns>
    Task<bool> MarkStaleAsync(CheckRecord check, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks stale every printed check whose check date is more than a number of days before today, in the site's time
    /// zone, such as from a workflow on a timer.
    /// </summary>
    /// <param name="ageInDays">How many days old a check must be, 1 or more.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many checks became stale.</returns>
    Task<int> MarkStaleChecksAsync(int ageInDays, CancellationToken cancellationToken = default);
}
