using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Transactions.Core;

/// <summary>
/// Defines the permissions used by the Transactions feature.
/// </summary>
public static class TransactionsPermissions
{
    /// <summary>
    /// The permission required to view and manage every tenant transaction, send reminders, and settle or
    /// cancel outstanding obligations from the administration report.
    /// </summary>
    public static readonly Permission ManageTransactions = new(
        "ManageTransactions",
        LocalizationSource.Create("Manage transactions", typeof(TransactionsPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// The permission required to configure the transaction reminder settings.
    /// </summary>
    public static readonly Permission ManageTransactionSettings = new(
        "ManageTransactionSettings",
        LocalizationSource.Create("Manage transaction settings", typeof(TransactionsPermissions)));

    /// <summary>
    /// The permission required to view the payment and refund ledgers and to issue a refund. It is separate
    /// from managing transactions because giving money back is the one action in the suite that cannot be
    /// undone, and it should not come free with the ability to read the outstanding report.
    /// </summary>
    public static readonly Permission ManageRefunds = new(
        "ManageRefunds",
        LocalizationSource.Create("Manage payments and refunds", typeof(TransactionsPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// The permission required for an authenticated user to view and pay their own transactions.
    /// </summary>
    public static readonly Permission ViewOwnTransactions = new(
        "ViewOwnTransactions",
        LocalizationSource.Create("View own transactions", typeof(TransactionsPermissions)));
}
