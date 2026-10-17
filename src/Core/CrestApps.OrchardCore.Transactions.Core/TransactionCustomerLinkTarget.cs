namespace CrestApps.OrchardCore.Transactions.Core;

/// <summary>
/// Which customer pages the links sent to a customer open.
/// </summary>
public enum TransactionCustomerLinkTarget
{
    /// <summary>
    /// The customer pages in the admin: <c>Admin/my-transactions</c>, <c>Admin/transaction-invoices/{itemId}</c> and
    /// <c>Admin/transaction-receipts/{itemId}/{paymentId}</c>. This is the default.
    /// </summary>
    Admin = 0,

    /// <summary>
    /// The customer pages in the site theme: <c>my-transactions</c>, <c>my-transactions/invoices/{itemId}</c> and
    /// <c>my-transactions/receipts/{itemId}/{paymentId}</c>.
    /// </summary>
    Site = 1,
}
