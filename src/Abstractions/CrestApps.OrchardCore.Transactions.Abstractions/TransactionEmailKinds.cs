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
    /// The receipt of a payment.
    /// </summary>
    public const string PaymentReceipt = "payment-receipt";

    /// <summary>
    /// The notice of a refund.
    /// </summary>
    public const string Refund = "payment-refund";
}
