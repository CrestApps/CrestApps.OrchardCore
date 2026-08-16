using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the printable receipt for a single subscription payment transaction.
/// </summary>
public class SubscriptionReceiptViewModel
{
    /// <summary>
    /// Gets or sets the display name of the site that issued the receipt.
    /// </summary>
    public string SiteName { get; set; }

    /// <summary>
    /// Gets or sets the display name of the subscriber the receipt is billed to.
    /// </summary>
    public string BilledToName { get; set; }

    /// <summary>
    /// Gets or sets the email address of the subscriber the receipt is billed to.
    /// </summary>
    public string BilledToEmail { get; set; }

    /// <summary>
    /// Gets or sets the title of the service plan billed by the transaction.
    /// </summary>
    public string ServicePlanTitle { get; set; }

    /// <summary>
    /// Gets or sets the transaction identifier printed on the receipt.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the local date and time the transaction was recorded.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the ISO currency code used for the transaction amounts.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the total amount captured by the transaction, including tax.
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Gets or sets the tax portion of <see cref="Amount"/>.
    /// </summary>
    public double TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the payment status of the transaction.
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the payment gateway that processed the transaction.
    /// </summary>
    public string GatewayId { get; set; }

    /// <summary>
    /// Gets or sets the environment mode used by the payment gateway for the transaction.
    /// </summary>
    public GatewayMode GatewayMode { get; set; }

    /// <summary>
    /// Gets or sets the immutable tax lines captured when the transaction was created.
    /// </summary>
    public IList<TaxLine> TaxLines { get; set; } = [];
}
