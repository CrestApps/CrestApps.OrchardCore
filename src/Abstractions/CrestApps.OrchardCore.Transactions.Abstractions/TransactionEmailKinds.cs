namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// The kinds of the messages a buyer gets about a transaction, which the store can write its own templates of. Each
/// template reads the transaction as <c>{{ Transaction }}</c>.
/// </summary>
public static class TransactionEmailKinds
{
    /// <summary>
    /// The name the store's template reads the transaction under.
    /// </summary>
    public const string TransactionModelName = "Transaction";

    /// <summary>
    /// A reminder of a payment that's coming up, due today or late.
    /// </summary>
    public const string PaymentReminder = "payment-reminder";

    /// <summary>
    /// The first request to pay a debt the payer pays themselves: the transaction's own invoice on a site without the
    /// Invoicing feature. It has a template of its own, so changing the reminder template does not change it.
    /// </summary>
    public const string PaymentRequest = "payment-request";

    /// <summary>
    /// The name of the template value that holds the link that pays the transaction without signing in, when one could be
    /// made.
    /// </summary>
    public const string PayUrlValueName = "PayUrl";

    /// <summary>
    /// The name of the template value that holds the link to the printable document of the transaction, when the payer
    /// can open one.
    /// </summary>
    public const string DocumentUrlValueName = "DocumentUrl";

    /// <summary>
    /// The name of the template value that holds the number of the document behind the transaction, such as an invoice
    /// number, when it has one.
    /// </summary>
    public const string DocumentNumberValueName = "DocumentNumber";

    /// <summary>
    /// The name of the template value that holds the amount still owed, formatted with its currency.
    /// </summary>
    public const string AmountDueValueName = "AmountDue";

    /// <summary>
    /// The name of the template value that holds the due date in the site's time zone, formatted as a short date, when the
    /// transaction has one.
    /// </summary>
    public const string DueDateValueName = "DueDate";

    /// <summary>
    /// The receipt of a payment.
    /// </summary>
    public const string PaymentReceipt = "payment-receipt";

    /// <summary>
    /// The notice of a refund.
    /// </summary>
    public const string Refund = "payment-refund";
}
