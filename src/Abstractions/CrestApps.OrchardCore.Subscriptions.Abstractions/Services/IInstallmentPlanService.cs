using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Creates installment plans and moves them through their life: taking the down payment, starting the schedule,
/// collecting each payment and finishing or canceling the plan. Money only ever moves through the checkout.
/// </summary>
public interface IInstallmentPlanService
{
    /// <summary>
    /// Validates the request and creates a plan waiting for its down payment, together with the down-payment
    /// transaction and the checkout session that takes it.
    /// </summary>
    /// <param name="request">What the administrator, or the module creating the plan, entered.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InstallmentPlanResult> CreateAsync(CreateInstallmentPlanRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns an outstanding transaction into an installment plan for its owner, while the customer is paying it at
    /// checkout. The plan pays for what the transaction paid for: it takes the transaction's reference, total and
    /// currency, and charges its first payment through the supplied checkout session, which is moved onto that payment
    /// and asked to keep the card for the rest. The transaction stays owed until the first payment is received and is
    /// canceled then, so an abandoned checkout changes nothing for the customer.
    /// </summary>
    /// <param name="request">The transaction, the installments the customer chose and the checkout session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The checkout session must still be waiting for the customer: nothing may have been charged through it yet.
    /// </remarks>
    Task<InstallmentPlanResult> CreateFromTransactionAsync(CreateInstallmentPlanFromTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the schedule of a plan whose down payment was received: keeps the payment method, creates a
    /// transaction for every scheduled payment and makes the plan active. Calling it again does nothing.
    /// </summary>
    /// <param name="planId">The plan identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InstallmentPlanResult> ActivateAsync(string planId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Charges the saved payment method for one payment now, through the checkout. Used by the schedule on the
    /// due date and by an administrator who wants to collect early or retry.
    /// </summary>
    /// <param name="planId">The plan identifier.</param>
    /// <param name="paymentNumber">The payment to charge.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InstallmentPlanResult> ChargeAsync(string planId, int paymentNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings the plan up to date with its transactions and the clock: payments received, payments now due, retries
    /// that are due, and the plan's resulting status. Safe to call at any time.
    /// </summary>
    /// <param name="planId">The plan identifier.</param>
    /// <param name="waitForLock">
    /// Whether to wait when another process is changing the plan. A caller reacting to a payment passes
    /// <see langword="false"/>: when the plan is busy, it is busy collecting that very payment and brings itself
    /// up to date when it finishes.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InstallmentPlanResult> ProcessAsync(string planId, bool waitForLock = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the plan. Payments already received stay received; payments not yet received are canceled.
    /// </summary>
    /// <param name="planId">The plan identifier.</param>
    /// <param name="reason">An optional reason recorded on the plan.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InstallmentPlanResult> CancelAsync(string planId, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// What an administrator, or a module creating a plan for its own record, enters to create an installment plan.
/// </summary>
public sealed class CreateInstallmentPlanRequest
{
    /// <summary>
    /// Gets or sets what the plan pays for.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the identifier of an existing customer. When empty, a customer is created from
    /// <see cref="NewCustomerName"/> and <see cref="NewCustomerEmail"/>.
    /// </summary>
    public string CustomerUserId { get; set; }

    /// <summary>
    /// Gets or sets the name of a customer to create.
    /// </summary>
    public string NewCustomerName { get; set; }

    /// <summary>
    /// Gets or sets the email of a customer to create.
    /// </summary>
    public string NewCustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the total paid over the life of the plan, the down payment included.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the amount collected now. Zero means no down payment: the first scheduled payment is then due
    /// at once and is collected in its place, and <see cref="FirstDueUtc"/> must not be after today.
    /// </summary>
    public decimal DownPaymentAmount { get; set; }

    /// <summary>
    /// Gets or sets the number of payments after the down payment.
    /// </summary>
    public int InstallmentCount { get; set; }

    /// <summary>
    /// Gets or sets how often those payments fall due.
    /// </summary>
    public InstallmentFrequency Frequency { get; set; }

    /// <summary>
    /// Gets or sets when the first of them falls due. It must be after today when there is a down payment. Without
    /// one, the first payment is due at once, so this is left unset or set to today.
    /// </summary>
    public DateTime FirstDueUtc { get; set; }

    /// <summary>
    /// Gets or sets how they are collected.
    /// </summary>
    public InstallmentCollectionMethod CollectionMethod { get; set; }

    /// <summary>
    /// Gets or sets an internal note.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the plan pays for, as the module that owns the record names it, for example
    /// <c>Registration</c>. It is copied onto the plan and, as the parent reference, onto every transaction of the plan.
    /// Set it together with <see cref="ReferenceId"/>, or leave both empty.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the record the plan pays for.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets values the module creating the plan attaches to it. They are copied onto the plan and onto every
    /// transaction of the plan, and only that module interprets them.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; }
}

/// <summary>
/// The outcome of an installment plan operation.
/// </summary>
public sealed class InstallmentPlanResult
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the plan, as it stands after the operation.
    /// </summary>
    public InstallmentPlan Plan { get; set; }

    /// <summary>
    /// Gets the problems, keyed by the request member they are about (empty for the request as a whole).
    /// </summary>
    public IList<KeyValuePair<string, string>> Errors { get; } = [];

    /// <summary>
    /// Adds a problem.
    /// </summary>
    /// <param name="member">The request member it is about, or empty.</param>
    /// <param name="message">The message.</param>
    public InstallmentPlanResult Fail(string member, string message)
    {
        Errors.Add(new KeyValuePair<string, string>(member ?? string.Empty, message));

        return this;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="plan">The plan.</param>
    public static InstallmentPlanResult Success(InstallmentPlan plan)
        => new() { Plan = plan };
}
