using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The view model used by an administrator to lower what is owed on a transaction: a discount, a waiver, a credit or a
/// write-off.
/// </summary>
public class AdjustTransactionViewModel
{
    /// <summary>
    /// Gets or sets the transaction identifier the adjustment is recorded against.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the amount taken off what is owed.
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets why the amount is taken off.
    /// </summary>
    public TransactionAdjustmentKind Kind { get; set; }

    /// <summary>
    /// Gets or sets an optional reason recorded with the adjustment.
    /// </summary>
    public string Reason { get; set; }
}
