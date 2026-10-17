namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Provides shared constant values for the provider-agnostic Transactions module.
/// </summary>
public static class TransactionsConstants
{
    /// <summary>
    /// The YesSql collection name used to store <see cref="Models.Transaction"/> documents.
    /// </summary>
    public const string CollectionName = "Transaction";

    /// <summary>
    /// The identifier of the site settings group used to configure transaction reminders.
    /// </summary>
    public const string SettingsGroupId = "transactions";

    /// <summary>
    /// The key of the checkout step that carries the balance of the transaction a settlement checkout pays. Another
    /// feature that changes what such a checkout collects, such as turning the balance into an installment plan,
    /// finds the step by this key.
    /// </summary>
    public const string SettlementStepKey = "TransactionSettlement";

    /// <summary>
    /// The configuration section that holds <c>TransactionCustomerPagesOptions</c>, under the tenant's
    /// <c>OrchardCore</c> section.
    /// </summary>
    public const string CustomerPagesConfigurationSection = "CrestApps:Transactions:CustomerPages";

    /// <summary>
    /// Contains the feature identifiers exposed by the Transactions module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The main Transactions feature that tracks, reports, and settles financial obligations.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Transactions";

        /// <summary>
        /// The opt-in feature that delivers outstanding-payment reminders through the notification system.
        /// It depends on <c>OrchardCore.Notifications</c> so reminders honor each owner's channel preference.
        /// </summary>
        public const string Notification = "CrestApps.OrchardCore.Transactions.Notification";

        public const string Receipts = "CrestApps.OrchardCore.Transactions.Receipts";
    }

    /// <summary>
    /// The names of the routes other features link to.
    /// </summary>
    public static class RouteNames
    {
        /// <summary>
        /// The public page a signed pay link opens, where the owner pays one transaction without signing in.
        /// </summary>
        public const string Pay = "TransactionsPay";

        /// <summary>
        /// The customer's statement of their own transactions in the site theme, at <c>my-transactions</c>.
        /// </summary>
        public const string Statement = "TransactionsStatement";

        /// <summary>
        /// The printable invoice of a customer's transaction in the site theme, at
        /// <c>my-transactions/invoices/{itemId}</c>.
        /// </summary>
        public const string Invoice = "TransactionsInvoice";

        /// <summary>
        /// The printable receipt of a payment on a customer's transaction in the site theme, at
        /// <c>my-transactions/receipts/{itemId}/{paymentId}</c>.
        /// </summary>
        public const string Receipt = "TransactionsReceipt";

        /// <summary>
        /// The customer's statement in the admin, at <c>Admin/my-transactions</c>.
        /// </summary>
        public const string AdminStatement = "MyTransactions";

        /// <summary>
        /// The printable invoice in the admin, at <c>Admin/transaction-invoices/{itemId}</c>.
        /// </summary>
        public const string AdminInvoice = "TransactionInvoice";

        /// <summary>
        /// The printable receipt in the admin, at <c>Admin/transaction-receipts/{itemId}/{paymentId}</c>.
        /// </summary>
        public const string AdminReceipt = "TransactionReceipt";
    }

    /// <summary>
    /// The shapes that show a customer their own transactions. A theme overrides how they look with a template of
    /// the same name; a module renders them with a model built by <c>ITransactionCustomerPageService</c>.
    /// </summary>
    public static class Shapes
    {
        /// <summary>
        /// One page of a customer's statement. Its model is <c>TransactionStatementViewModel</c>.
        /// </summary>
        public const string Statement = "TransactionStatement";

        /// <summary>
        /// The printable invoice of a transaction. Its model is <c>TransactionInvoiceViewModel</c>.
        /// </summary>
        public const string Invoice = "TransactionInvoice";

        /// <summary>
        /// The printable receipt of one payment on a transaction. Its model is <c>TransactionReceiptViewModel</c>.
        /// </summary>
        public const string Receipt = "TransactionReceipt";
    }

    /// <summary>
    /// The canonical, well-known values for <see cref="Models.Transaction.ReferenceType"/>.
    /// </summary>
    public static class ReferenceTypes
    {
        /// <summary>
        /// A transaction ledger entry. Used when a settlement checkout is started to pay an outstanding
        /// transaction, so the completing checkout can be correlated back to the transaction it settles.
        /// </summary>
        public const string Transaction = "Transaction";
    }

    /// <summary>
    /// The well-known settlement methods recorded on a settled transaction.
    /// </summary>
    public static class SettlementMethods
    {
        /// <summary>
        /// The transaction was settled online through a payment provider.
        /// </summary>
        public const string Online = "online";

        /// <summary>
        /// The transaction was settled offline and recorded manually by a manager.
        /// </summary>
        public const string Offline = "offline";

        /// <summary>
        /// Nothing more was owed once an adjustment (a discount, waiver, credit or write-off) was recorded.
        /// </summary>
        public const string Adjusted = "adjusted";
    }
}
