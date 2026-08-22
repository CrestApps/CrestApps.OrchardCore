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
    public static readonly Permission ManageTransactions = new("ManageTransactions", "Manage transactions");

    /// <summary>
    /// The permission required to configure the transaction reminder settings.
    /// </summary>
    public static readonly Permission ManageTransactionSettings = new("ManageTransactionSettings", "Manage transaction settings");

    /// <summary>
    /// The permission required for an authenticated user to view and pay their own transactions.
    /// </summary>
    public static readonly Permission ViewOwnTransactions = new("ViewOwnTransactions", "View own transactions");
}
