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
    /// Contains the feature identifiers exposed by the Transactions module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The main Transactions feature that tracks, reports, and settles financial obligations.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Transactions";
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
    }
}
