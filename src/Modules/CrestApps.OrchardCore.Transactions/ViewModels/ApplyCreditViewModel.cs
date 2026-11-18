using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The customer credit staff apply toward a transaction.
/// </summary>
public class ApplyCreditViewModel
{
    /// <summary>
    /// Gets or sets the transaction identifier.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the amount of credit to apply.
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets an optional note recorded with the payment.
    /// </summary>
    public string Note { get; set; }
}
