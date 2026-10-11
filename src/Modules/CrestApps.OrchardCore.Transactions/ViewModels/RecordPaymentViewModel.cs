using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The view model used by an administrator to record an offline payment against a transaction.
/// </summary>
public class RecordPaymentViewModel
{
    /// <summary>
    /// Gets or sets the transaction identifier the payment is recorded against.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the amount being recorded as paid.
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets an optional note describing how the payment was received.
    /// </summary>
    public string Note { get; set; }
}
