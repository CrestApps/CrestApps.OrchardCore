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

        /// <summary>
        /// The catalog of payment terms (net days, due on receipt, end of month, a day of the next month, and an
        /// early-payment discount) that invoices, account terms, pay-later purchases and work orders read.
        /// </summary>
        public const string PaymentTerms = "CrestApps.OrchardCore.Transactions.PaymentTerms";
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
    /// The display types of a transaction other modules add shapes to.
    /// </summary>
    public static class DisplayTypes
    {
        /// <summary>
        /// The administration detail page of one transaction. Its zones are <c>Content</c> (the main column) and
        /// <c>Actions</c> (the side column).
        /// </summary>
        public const string DetailAdmin = "DetailAdmin";
    }

    /// <summary>
    /// The names of the events the ledger publishes to webhooks when the Commerce Webhooks feature is on.
    /// </summary>
    public static class WebhookEvents
    {
        /// <summary>
        /// A transaction was recorded.
        /// </summary>
        public const string Created = "transaction.created";

        /// <summary>
        /// A payment was applied to a transaction, online or recorded by staff.
        /// </summary>
        public const string PaymentRecorded = "transaction.payment-recorded";

        /// <summary>
        /// Nothing more is owed on a transaction.
        /// </summary>
        public const string Settled = "transaction.settled";

        /// <summary>
        /// A transaction was found unpaid after its due date, for the first time.
        /// </summary>
        public const string Overdue = "transaction.overdue";

        /// <summary>
        /// A transaction was canceled.
        /// </summary>
        public const string Canceled = "transaction.canceled";

        /// <summary>
        /// Money was given back on a transaction.
        /// </summary>
        public const string Refunded = "transaction.refunded";

        /// <summary>
        /// A payment recorded by hand or an adjustment was taken back with a reversing entry, so it is owed again.
        /// </summary>
        public const string EntryReversed = "transaction.entry-reversed";
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

        /// <summary>
        /// The payer's customer credit paid toward the transaction.
        /// </summary>
        public const string CustomerCredit = "customer-credit";
    }

    /// <summary>
    /// The business document kinds the ledger prints, each with its own document template.
    /// </summary>
    public static class BusinessDocumentKinds
    {
        /// <summary>
        /// The payment request of a transaction its owner pays themselves, shown to customers as its invoice.
        /// </summary>
        public const string PaymentRequest = "PaymentRequest";
    }

    /// <summary>
    /// The stable keys of the common ways money recorded by hand arrives (<see cref="Models.TransactionEvent.PaymentMethod"/>).
    /// The record payment form offers them, staff may still type another, and a report groups payments by these keys
    /// whatever the language of the person who recorded them.
    /// </summary>
    public static class OfflinePaymentMethods
    {
        /// <summary>
        /// Cash.
        /// </summary>
        public const string Cash = "cash";

        /// <summary>
        /// A check.
        /// </summary>
        public const string Check = "check";

        /// <summary>
        /// A bank transfer.
        /// </summary>
        public const string BankTransfer = "bank-transfer";

        /// <summary>
        /// A card taken on a card terminal in person.
        /// </summary>
        public const string CardTerminal = "card-terminal";

        /// <summary>
        /// Any other way.
        /// </summary>
        public const string Other = "other";

        /// <summary>
        /// The keys, in the order they are offered.
        /// </summary>
        public static readonly IReadOnlyList<string> All = [Cash, Check, BankTransfer, CardTerminal, Other];
    }
}
