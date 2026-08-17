using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The view model for the transaction detail screen, shared by the administration and customer views.
/// </summary>
public class TransactionDetailViewModel
{
    /// <summary>
    /// Gets or sets the transaction being displayed.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets the display name of the transaction owner, when it could be resolved.
    /// </summary>
    public string OwnerName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current user may manage the transaction (send reminders,
    /// record payments, and cancel it).
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether payment reminders can be sent. This is <c>true</c> only when
    /// the Transaction Reminders feature is enabled.
    /// </summary>
    public bool CanSendReminder { get; set; }
}
