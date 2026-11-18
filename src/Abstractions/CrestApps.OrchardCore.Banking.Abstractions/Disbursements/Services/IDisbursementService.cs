using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.SourceAccess;

namespace CrestApps.OrchardCore.Banking.Disbursements.Services;

/// <summary>
/// The rules of disbursements: idempotent recording, batches, release through a provider, the status table, returns and
/// change notices, and who is told. It checks no permission; the caller does.
/// </summary>
public interface IDisbursementService
{
    /// <summary>
    /// Gets the providers the enabled features registered.
    /// </summary>
    IReadOnlyList<IDisbursementProvider> Providers { get; }

    /// <summary>
    /// Records a disbursement, or returns the one its idempotency key already gave. The key is the attempt: asking
    /// again with it and the same payee, amount, method and destination gives the same disbursement; asking with it and
    /// anything else is refused; and once that disbursement failed, was cancelled, returned or reversed, a new attempt
    /// needs a new key, and asking with the old one says so.
    /// </summary>
    /// <param name="request">What to pay.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The disbursement, or why it was refused.</returns>
    Task<DisbursementResult> CreateAsync(DisbursementRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a disbursement that was not sent yet.
    /// </summary>
    /// <param name="disbursement">The disbursement.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when it can't be cancelled.</returns>
    Task<bool> CancelAsync(Disbursement disbursement, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts pending disbursements of one source account and one provider's method in a new batch.
    /// </summary>
    /// <param name="providerName">The provider.</param>
    /// <param name="disbursementIds">The disbursements.</param>
    /// <param name="effectiveDate">The date the money should arrive.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The batch, and why some disbursements were left out.</returns>
    Task<(DisbursementBatch Batch, IReadOnlyList<string> Problems)> CreateBatchAsync(string providerName, IEnumerable<string> disbursementIds, DateOnly effectiveDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a batch to its provider: the file is written and stored, the checks issued, or the payouts sent.
    /// </summary>
    /// <param name="batch">The batch.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Why it could not be released; empty when it was.</returns>
    Task<IReadOnlyList<string>> ReleaseBatchAsync(DisbursementBatch batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an open batch; its disbursements go back to pending.
    /// </summary>
    /// <param name="batch">The batch.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when it was already released.</returns>
    Task<bool> CancelBatchAsync(DisbursementBatch batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the submitted disbursements of a released batch settled.
    /// </summary>
    /// <param name="batch">The batch.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many disbursements settled.</returns>
    Task<int> SettleBatchAsync(DisbursementBatch batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles every released batch released more than a number of days ago, such as from a workflow on a timer once
    /// the bank's return window has passed.
    /// </summary>
    /// <param name="ageInDays">How many days ago a batch must have been released, 1 or more.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many disbursements settled.</returns>
    Task<int> SettleReleasedBatchesAsync(int ageInDays, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a disbursement settled.
    /// </summary>
    /// <param name="disbursement">The disbursement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the move is not allowed.</returns>
    Task<bool> MarkSettledAsync(Disbursement disbursement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a disbursement failed or was returned.
    /// </summary>
    /// <param name="disbursement">The disbursement.</param>
    /// <param name="status"><see cref="DisbursementStatus.Returned"/>, <see cref="DisbursementStatus.Failed"/>, <see cref="DisbursementStatus.Canceled"/> or <see cref="DisbursementStatus.Reversed"/>.</param>
    /// <param name="code">The return or failure code.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the move is not allowed.</returns>
    Task<bool> MarkClosedAsync(Disbursement disbursement, DisbursementStatus status, string code, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the returns of a bank's return file: each matching disbursement is marked returned.
    /// </summary>
    /// <param name="returns">The returns.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was applied.</returns>
    Task<DisbursementImportResult> ApplyReturnsAsync(IEnumerable<DisbursementReturn> returns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a bank's notices of change: each payee bank account is corrected, which takes it back to unverified.
    /// </summary>
    /// <param name="notices">The notices.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was applied.</returns>
    Task<DisbursementImportResult> ApplyChangeNoticesAsync(IEnumerable<DisbursementChangeNotice> notices, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batches pending disbursements with the entry description a bank file prints for them, such as PAYROLL.
    /// </summary>
    /// <param name="providerName">The provider that sends the batch.</param>
    /// <param name="disbursementIds">The disbursements, in the order they are sent.</param>
    /// <param name="effectiveDate">The day the money should arrive: today or later.</param>
    /// <param name="entryDescription">The entry description, or <see langword="null"/> for the bank account's own.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The batch, or <see langword="null"/>, and what was left out and why.</returns>
    Task<(DisbursementBatch Batch, IReadOnlyList<string> Problems)> CreateBatchAsync(string providerName, IEnumerable<string> disbursementIds, DateOnly effectiveDate, string entryDescription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a bank's returns to the disbursements the filter allows; the others are reported as not found.
    /// </summary>
    /// <param name="returns">The returns.</param>
    /// <param name="access">The disbursements the person may act on.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was applied.</returns>
    Task<DisbursementImportResult> ApplyReturnsAsync(IEnumerable<DisbursementReturn> returns, SourceAccessFilter access, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a bank's notices of change to the disbursements the filter allows, when the person may change bank
    /// accounts; otherwise each is reported for someone who may.
    /// </summary>
    /// <param name="notices">The notices.</param>
    /// <param name="access">The disbursements the person may act on.</param>
    /// <param name="canChangeBankAccounts">Whether the person may change bank accounts.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was applied.</returns>
    Task<DisbursementImportResult> ApplyChangeNoticesAsync(IEnumerable<DisbursementChangeNotice> notices, SourceAccessFilter access, bool canChangeBankAccounts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens a batch left <see cref="DisbursementBatchStatus.Releasing"/>, once someone checked with the provider that
    /// nothing was sent. The next release sends it again.
    /// </summary>
    /// <param name="batch">The batch.</param>
    /// <param name="reason">What was checked.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the batch is not being released.</returns>
    Task<bool> ReopenReleasingBatchAsync(DisbursementBatch batch, string reason, CancellationToken cancellationToken = default);
}
