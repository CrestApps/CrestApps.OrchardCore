using System.Text.Json.Serialization;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// A payment plan for a customer: an optional down payment taken when the plan is created, then a fixed number of
/// payments on a schedule. When there is no down payment, the first scheduled payment is due at once and is taken in
/// its place. Every payment is an ordinary ledger transaction, so the plan only describes the arrangement and tracks
/// where each payment stands. A plan is set up by an administrator, by another module through
/// <c>IInstallmentPlanService</c>, or by the customer at checkout when they choose to pay a balance in installments.
/// </summary>
/// <remarks>
/// The values in the inherited <c>Properties</c> belong to the module that created the plan; they are copied onto
/// every transaction of the plan.
/// </remarks>
public sealed class InstallmentPlan : CatalogItem
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets what the plan pays for, shown to the customer on every payment.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the plan pays for, as the module that owns the record names it (for example a
    /// registration). It is copied onto every transaction of the plan as its parent reference, so the module finds
    /// its record from any payment.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the record the plan pays for.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the outstanding transaction the plan replaces, when the customer chose at checkout to pay it in
    /// installments. The transaction is canceled once the plan's first payment is received; until then it stays
    /// owed, so an abandoned checkout leaves the customer's balance as it was.
    /// </summary>
    public string SourceTransactionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user the plan belongs to.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the customer's name as it was when the plan was created, for lists and receipts.
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Gets or sets the customer's email as it was when the plan was created.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency of every amount on the plan.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the total the customer pays over the life of the plan, the down payment included.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the amount collected when the plan is created, or zero for a plan without a down payment, whose
    /// first scheduled payment is collected then instead.
    /// </summary>
    public decimal DownPaymentAmount { get; set; }

    /// <summary>
    /// Gets or sets the number of payments after the down payment.
    /// </summary>
    public int InstallmentCount { get; set; }

    /// <summary>
    /// Gets or sets how often the payments after the down payment fall due.
    /// </summary>
    public InstallmentFrequency Frequency { get; set; }

    /// <summary>
    /// Gets or sets when the first payment after the down payment falls due. On a plan without a down payment it is
    /// when the plan was created, because that payment is collected at once.
    /// </summary>
    public DateTime FirstDueUtc { get; set; }

    /// <summary>
    /// Gets or sets how the payments after the down payment are collected.
    /// </summary>
    public InstallmentCollectionMethod CollectionMethod { get; set; }

    /// <summary>
    /// Gets or sets where the plan stands.
    /// </summary>
    public InstallmentPlanStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the payment method kept when the first payment was taken, which the scheduled payments are
    /// charged to when they are collected automatically.
    /// </summary>
    public SavedPaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets the checkout session that takes the first payment: the down payment, or the first scheduled
    /// payment of a plan without one. The payment method kept by this checkout is the one the schedule charges.
    /// </summary>
    public string DownPaymentSessionId { get; set; }

    /// <summary>
    /// Gets the schedule: the down payment first (number 0), then each scheduled payment in order.
    /// </summary>
    public IList<InstallmentPlanPayment> Payments { get; init; } = [];

    /// <summary>
    /// Gets the history of the plan.
    /// </summary>
    public IList<InstallmentPlanEvent> Events { get; init; } = [];

    /// <summary>
    /// Gets or sets an internal note about the plan.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the person who created the plan: an administrator, or the customer who chose
    /// installments at checkout.
    /// </summary>
    public string CreatedById { get; set; }

    /// <summary>
    /// Gets or sets the name of the person who created the plan.
    /// </summary>
    public string CreatedByName { get; set; }

    /// <summary>
    /// Gets or sets when the plan was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the plan was last changed.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the first payment was received and the schedule started.
    /// </summary>
    public DateTime? ActivatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last payment was received.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the plan was canceled.
    /// </summary>
    public DateTime? CanceledUtc { get; set; }

    /// <summary>
    /// Gets the down payment, or <see langword="null"/> on a plan that has none recorded.
    /// </summary>
    /// <remarks>
    /// This and the other computed properties are not stored. They return payments the plan already holds, and the
    /// document serializer populates existing objects when it reads, so a stored copy would be read back into the
    /// same payment and add to its lists on every save.
    /// </remarks>
    [JsonIgnore]
    public InstallmentPlanPayment DownPayment
        => Payments.FirstOrDefault(payment => payment.Number == 0);

    /// <summary>
    /// Gets the payment taken when the plan is created, which starts the schedule once it is received: the down
    /// payment, or the first scheduled payment of a plan without one. <see langword="null"/> on a plan with no payments.
    /// </summary>
    [JsonIgnore]
    public InstallmentPlanPayment FirstPayment
        => Payments.OrderBy(payment => payment.Number).FirstOrDefault();

    /// <summary>
    /// Gets the total received so far.
    /// </summary>
    [JsonIgnore]
    public decimal AmountPaid
        => Payments.Where(payment => payment.Status == InstallmentPaymentStatus.Paid).Sum(payment => payment.Amount);

    /// <summary>
    /// Gets the total still to be received, excluding anything canceled.
    /// </summary>
    [JsonIgnore]
    public decimal AmountOutstanding
        => Payments.Where(payment => payment.Status is not InstallmentPaymentStatus.Paid and not InstallmentPaymentStatus.Canceled).Sum(payment => payment.Amount);

    /// <summary>
    /// Gets the next payment still to be received, or <see langword="null"/> when none is.
    /// </summary>
    [JsonIgnore]
    public InstallmentPlanPayment NextPayment
        => Payments
            .Where(payment => payment.Status is not InstallmentPaymentStatus.Paid and not InstallmentPaymentStatus.Canceled)
            .OrderBy(payment => payment.Number)
            .FirstOrDefault();
}
