using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The page a signed pay link opens.
/// </summary>
public class TransactionPayViewModel
{
    /// <summary>
    /// Gets or sets the signed token from the link, posted back to pay.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the transaction the link names.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets the transaction's invoice, or <see langword="null"/> when invoices are not available.
    /// </summary>
    public BusinessDocument Invoice { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the transaction can be paid online now.
    /// </summary>
    public bool CanPay { get; set; }

    /// <summary>
    /// Gets or sets whether the payer just asked to pay less than the smallest part payment the transaction accepts,
    /// so the page says what the smallest payment is.
    /// </summary>
    public bool AmountTooSmall { get; set; }
}
