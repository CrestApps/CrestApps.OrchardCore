using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// A durable, provider-agnostic ledger entry for a single financial obligation. A transaction is the
/// customer- and administrator-facing record of "money owed": it is created whenever a purchase is
/// committed without being settled immediately (for example an offline Pay Later commitment), and it is
/// updated as reminders are sent and payments are recorded until it reaches a terminal state.
/// </summary>
public sealed class Transaction : CatalogItem, ITimestampedModel
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
    /// Gets or sets the kind of party that owes the money, one of the party kinds such as <c>Account</c>,
    /// <c>Customer</c>, <c>User</c> or <c>Contact</c>. Together with <see cref="PayerId"/> it names who the debt belongs
    /// to, which is not always the person who may pay it: an account owes its invoices, and each of its members may pay
    /// them. Read it with <see cref="TransactionPayerExtensions.GetPayer"/>, which also answers for transactions created
    /// before the payer was recorded.
    /// </summary>
    public string PayerKind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the party named by <see cref="PayerKind"/>.
    /// </summary>
    public string PayerId { get; set; }

    /// <summary>
    /// Gets or sets the payer's name, e-mail address, phone number and billing address as they were when the debt was
    /// recorded, so the payer can be addressed without looking the party up, and so a later change to the party does not
    /// change what the ledger says about this debt. <see langword="null"/> when none was recorded.
    /// </summary>
    public ContactInfo PayerContact { get; set; }

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
    /// Gets or sets the kind of record the transaction ultimately belongs to, when its own reference names an
    /// intermediate record. For example, a payment of an installment plan references the plan, and its parent
    /// reference names the record the plan pays for, such as a registration. It is indexed, so the module that owns
    /// that record finds it from any of the payments without a mapping table of its own.
    /// </summary>
    public string ParentReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the record named by <see cref="ParentReferenceType"/>.
    /// </summary>
    public string ParentReferenceId { get; set; }

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
    /// Gets or sets the tax owed, broken down by tax and rate, when whoever recorded the debt knew the breakdown. The
    /// lines add up to <see cref="TaxAmount"/>. It is empty for a debt recorded with a tax total only.
    /// </summary>
    public IList<TaxLine> TaxLines { get; set; } = [];

    /// <summary>
    /// Gets or sets the rate that turns the amounts of the transaction into <see cref="BaseCurrency"/>, fixed when the
    /// debt was recorded: one unit of <see cref="Currency"/> is this many units of the base currency.
    /// <see langword="null"/> when the transaction is in the base currency or no rate was recorded.
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 code of the currency the site reports in, which <see cref="ExchangeRate"/> converts to.
    /// <see langword="null"/> when no rate was recorded.
    /// </summary>
    public string BaseCurrency { get; set; }

    /// <summary>
    /// Gets or sets the total amount owed including tax, in <see cref="Currency"/>.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the amount that has been paid so far, in <see cref="Currency"/>.
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Gets or sets the amount taken off what is owed without money being received, in <see cref="Currency"/>: discounts,
    /// waivers, credits and write-offs recorded as <see cref="TransactionEventType.Adjusted"/> events. It lowers
    /// <see cref="OutstandingAmount"/> but is never counted as paid.
    /// </summary>
    public decimal AmountAdjusted { get; set; }

    /// <summary>
    /// Gets the amount still owed, in <see cref="Currency"/>: the total less what was adjusted and what was paid. Never
    /// negative.
    /// </summary>
    public decimal OutstandingAmount
        => Math.Max(0m, TotalAmount - AmountAdjusted - AmountPaid);

    /// <summary>
    /// Gets or sets the amount given back to the customer so far, in <see cref="Currency"/>. A refund doesn't
    /// reduce <see cref="AmountPaid"/>, so a refunded transaction never turns collectable again.
    /// </summary>
    public decimal AmountRefunded { get; set; }

    /// <summary>
    /// Gets or sets whether the payer may pay part of what is owed online, rather than all of it at once. Staff can
    /// always record a part payment received offline.
    /// </summary>
    public bool AllowPartialPayment { get; set; }

    /// <summary>
    /// Gets or sets the smallest part payment accepted online, in <see cref="Currency"/>, when
    /// <see cref="AllowPartialPayment"/> is set. <see langword="null"/> accepts any amount. What is still owed can always
    /// be paid in full, even when it is less than this.
    /// </summary>
    public decimal? MinimumPayment { get; set; }

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
    /// Gets or sets the UTC time the reminder that the transaction is coming due was sent, when it was. Only one
    /// such reminder is sent before the due date; reminders after it are counted by <see cref="ReminderCount"/>.
    /// </summary>
    public DateTime? UpcomingReminderSentUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the reminder sweep first found the transaction unpaid after the day it was due, and
    /// raised <see cref="Services.ITransactionEventHandler.BecameOverdueAsync"/>. It is set once and never cleared, so
    /// a transaction is reported overdue only once.
    /// </summary>
    public DateTime? BecameOverdueUtc { get; set; }

    /// <summary>
    /// Gets or sets the invoice number, for example <c>INV-1001</c>, of a transaction the owner pays themselves. It is
    /// issued the first time the owner is told about the payment and never changes after that.
    /// </summary>
    public string InvoiceNumber { get; set; }

    /// <summary>
    /// Gets or sets how the transaction will be collected without the owner acting, when it will be: for example
    /// a saved card charged on the due date. <see langword="null"/> means the owner pays it themselves.
    /// </summary>
    public TransactionAutoCollection AutoCollection { get; set; }

    /// <summary>
    /// Gets the audit timeline of the transaction.
    /// </summary>
    public IList<TransactionEvent> Events { get; init; } = [];

    /// <summary>
    /// Gets or sets the billing cycle this transaction covers, when it was created by a recurring
    /// agreement. It is <see langword="null"/> for a one-time obligation.
    /// </summary>
    public TransactionRecurrence Recurrence { get; set; }
}
