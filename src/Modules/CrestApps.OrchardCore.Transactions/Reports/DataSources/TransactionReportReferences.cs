using CrestApps.OrchardCore.Checkout;

namespace CrestApps.OrchardCore.Transactions.Reports.DataSources;

/// <summary>
/// Reads which ledger transaction or order a payment or refund was for, from the reference it carries.
/// </summary>
internal static class TransactionReportReferences
{
    /// <summary>
    /// Gets the transaction a reference names, or <see langword="null"/> when it names something else.
    /// </summary>
    /// <param name="referenceType">The kind of thing the reference names.</param>
    /// <param name="referenceId">The identifier of the thing.</param>
    /// <returns>The identifier of the transaction, or <see langword="null"/>.</returns>
    public static string TransactionId(string referenceType, string referenceId)
    {
        return string.Equals(referenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase)
            ? referenceId
            : null;
    }

    /// <summary>
    /// Gets the order a reference names, or <see langword="null"/> when it names something else.
    /// </summary>
    /// <param name="referenceType">The kind of thing the reference names.</param>
    /// <param name="referenceId">The identifier of the thing.</param>
    /// <returns>The identifier of the order, or <see langword="null"/>.</returns>
    public static string OrderId(string referenceType, string referenceId)
    {
        return string.Equals(referenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal)
            ? referenceId
            : null;
    }
}
