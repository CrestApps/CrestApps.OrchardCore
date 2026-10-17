namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The outcome of building a customer document with <see cref="ITransactionCustomerPageService"/>.
/// </summary>
public enum TransactionCustomerPageStatus
{
    /// <summary>
    /// The document was built and the viewer may see it.
    /// </summary>
    Found = 0,

    /// <summary>
    /// The viewer may not see their transactions at all: they have neither the "View own transactions" nor the
    /// "Manage transactions" permission, or they are not signed in.
    /// </summary>
    Forbidden = 1,

    /// <summary>
    /// The document does not exist, belongs to someone else, or cannot be built on this site.
    /// </summary>
    NotFound = 2,
}
