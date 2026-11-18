using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Charges what a transaction owes to its owner's saved payment method on a day, and tries again on the retry schedule
/// of the reminder settings when the charge is declined. When the retries run out, or the card's bank asks the
/// cardholder to approve the payment, the transaction is handed to its owner to pay: the charge is removed, a payment
/// issue is recorded, and <see cref="ITransactionEventHandler.AutoChargeAbandonedAsync"/> is raised so the module that
/// owns the debt can send its own notice. The charge itself is <see cref="ITransactionCommandService.ChargeSavedPaymentMethodAsync"/>.
/// </summary>
public interface ISavedPaymentMethodChargeService
{
    /// <summary>
    /// Schedules the charge of a transaction to its owner's saved payment method, replacing any charge scheduled before.
    /// The transaction then names the card in its upcoming reminder and gets no overdue reminder while the charge and
    /// its retries are pending.
    /// </summary>
    /// <param name="transactionId">The transaction to charge.</param>
    /// <param name="savedPaymentMethodId">The identifier of the owner's saved payment method, or <see langword="null"/> for the owner's default one; refused when the owner kept none that can be charged.</param>
    /// <param name="chargeOnUtc">When to charge it, in UTC; now or earlier charges it on the next sweep.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<SavedPaymentMethodChargeResult> ScheduleAsync(string transactionId, string savedPaymentMethodId, DateTime chargeOnUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules the charge of a transaction as the request says: to one of the owner's saved cards or the card a
    /// checkout kept, on a day, retried on the request's own schedule or the reminder settings' one. It replaces any
    /// charge scheduled before, and its declined charges count from zero again. A module that owns a debt with its own
    /// card and retry schedule, such as an installment plan or a booking balance, schedules it this way, and a module
    /// that creates the transaction can instead set <see cref="Transaction.AutoCharge"/> on it before saving it.
    /// </summary>
    /// <param name="request">What to charge, with which card, when, and how to retry.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<SavedPaymentMethodChargeResult> ScheduleAsync(SavedPaymentMethodChargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Charges a transaction whose charge is scheduled, now, and records what came of it: removed when it went through,
    /// moved to the next retry when it was declined, or handed to the owner when no retry is left.
    /// </summary>
    /// <param name="transactionId">The transaction to charge.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<SavedPaymentMethodChargeResult> ChargeAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Charges the transactions whose scheduled charge is due. Returns how many were charged successfully.
    /// </summary>
    /// <param name="utcNow">The time to compare the scheduled charges with.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<int> ChargeDueAsync(DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the charge scheduled on a transaction, so its owner pays it themselves.
    /// </summary>
    /// <param name="transactionId">The transaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<SavedPaymentMethodChargeResult> CancelAsync(string transactionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A charge of a transaction to schedule with <see cref="ISavedPaymentMethodChargeService.ScheduleAsync(SavedPaymentMethodChargeRequest, CancellationToken)"/>.
/// </summary>
public sealed class SavedPaymentMethodChargeRequest
{
    /// <summary>
    /// Gets or sets the transaction to charge.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the owner's saved payment method to charge. When it and
    /// <see cref="SourceSessionId"/> are both empty, the owner's default saved card is charged.
    /// </summary>
    public string SavedPaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the checkout whose kept card is charged, when <see cref="SavedPaymentMethodId"/> is empty. It must
    /// belong to the transaction's owner (or, for a guest's transaction, be a guest's checkout too). It is only taken from
    /// server code that recorded the checkout itself, never from a browser.
    /// </summary>
    public string SourceSessionId { get; set; }

    /// <summary>
    /// Gets or sets when to charge it, in UTC; now or earlier charges it on the next sweep.
    /// </summary>
    public DateTime ChargeOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the days between declined charges, one entry per retry, or <see langword="null"/> for the retry
    /// schedule of the reminder settings. See <see cref="TransactionAutoCharge.RetryDays"/>.
    /// </summary>
    public int[] RetryDays { get; set; }
}

/// <summary>
/// What a scheduled charge of a saved payment method came to.
/// </summary>
public sealed class SavedPaymentMethodChargeResult
{
    /// <summary>
    /// Gets a value indicating whether the command did what was asked: scheduled, charged or canceled.
    /// </summary>
    public bool Succeeded
        => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the transaction as it stands after the command.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets how the charge ended, when one was made.
    /// </summary>
    public SavedPaymentMethodChargeStatus? ChargeStatus { get; set; }

    /// <summary>
    /// Gets or sets when the charge is tried again, when it is.
    /// </summary>
    public DateTime? NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the transaction was handed to its owner to pay, because no retry was left
    /// or the card needs its holder's approval.
    /// </summary>
    public bool HandedOver { get; set; }

    /// <summary>
    /// Gets the reasons the command did not do what was asked.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Adds a reason and returns the result.
    /// </summary>
    /// <param name="error">Why the command did not do what was asked.</param>
    public SavedPaymentMethodChargeResult Fail(string error)
    {
        Errors.Add(error);

        return this;
    }
}
