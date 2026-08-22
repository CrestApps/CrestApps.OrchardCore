using CrestApps.Core.Models;
using CrestApps.OrchardCore.Customers.Models;

namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// A durable, provider-agnostic ledger entry for a single financial obligation. A transaction is the
/// customer- and administrator-facing record of "money owed": it is created whenever a purchase is
/// committed without being settled immediately (for example an offline Pay Later commitment), and it is
/// updated as reminders are sent and payments are recorded until it reaches a terminal state.
/// </summary>
public sealed class Transaction : CatalogItem
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets a short human-readable title shown in listings and reminders.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the origin of the transaction, typically the payment provider key that created it (for
    /// example the Pay Later processor key). This keeps the ledger provider-agnostic while still allowing a
    /// report to be grouped or filtered by where an obligation came from.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user that owns the obligation. For a guest obligation this is the
    /// stable, tenant-scoped guest customer id rather than a user id.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets whether the owner is an authenticated user or a guest. Defaults to
    /// <see cref="CustomerOwnerKind.Authenticated"/> so obligations created before guest ownership existed
    /// keep their authenticated meaning.
    /// </summary>
    public CustomerOwnerKind OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the display name captured for a guest owner at purchase time, so the obligation can be
    /// addressed for reminders without a user account. Ignored for authenticated owners.
    /// </summary>
    public string GuestContactName { get; set; }

    /// <summary>
    /// Gets or sets the email address captured for a guest owner at purchase time, so the obligation can be
    /// reached for reminders without a user account. Ignored for authenticated owners.
    /// </summary>
    public string GuestContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the kind of thing the transaction is for (for example an order or a subscription).
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the thing the transaction is for.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets an optional secondary identifier of the thing the transaction is for.
    /// </summary>
    public string ReferenceVersionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the checkout session that created the transaction, when applicable.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the payment obligation the transaction settles within its originating
    /// checkout, when applicable. Combined with <see cref="CheckoutSessionId"/> it makes creation idempotent.
    /// </summary>
    public string ObligationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the confirmed payment attempt that settled (or last paid down) the
    /// transaction, when applicable. This is the canonical link from a settled obligation back to the
    /// durable payment ledger, so a settlement can always be reconciled against the gateway.
    /// </summary>
    public string PaymentAttemptId { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code of the amounts recorded on the transaction.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the amount owed before tax, in <see cref="Currency"/>.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the tax owed, in <see cref="Currency"/>.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the total amount owed including tax, in <see cref="Currency"/>.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the amount that has been paid so far, in <see cref="Currency"/>.
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Gets the amount still owed, in <see cref="Currency"/>. Never negative.
    /// </summary>
    public decimal OutstandingAmount
        => Math.Max(0m, TotalAmount - AmountPaid);

    /// <summary>
    /// Gets or sets the current lifecycle state of the transaction.
    /// </summary>
    public TransactionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the transaction was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the transaction was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time by which the transaction should be paid, when applicable.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the transaction was fully settled, when applicable.
    /// </summary>
    public DateTime? SettledUtc { get; set; }

    /// <summary>
    /// Gets or sets the reference used to settle the transaction (for example the settlement checkout
    /// session id or a provider transaction id), when applicable.
    /// </summary>
    public string SettlementReference { get; set; }

    /// <summary>
    /// Gets or sets how the transaction was settled, using one of the
    /// <see cref="TransactionsConstants.SettlementMethods"/> values, when applicable.
    /// </summary>
    public string SettlementMethod { get; set; }

    /// <summary>
    /// Gets or sets the number of reminders that have been sent to the owner.
    /// </summary>
    public int ReminderCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the last reminder was sent, when applicable.
    /// </summary>
    public DateTime? LastReminderSentUtc { get; set; }

    /// <summary>
    /// Gets the audit timeline of the transaction.
    /// </summary>
    public IList<TransactionEvent> Events { get; init; } = [];
}
