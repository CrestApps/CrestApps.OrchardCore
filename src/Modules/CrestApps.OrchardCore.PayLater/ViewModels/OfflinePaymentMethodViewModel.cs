namespace CrestApps.OrchardCore.PayLater.ViewModels;

/// <summary>
/// An offline payment method's panel, or how to pay an order placed with it.
/// </summary>
public class OfflinePaymentMethodViewModel
{
    /// <summary>
    /// Gets or sets the store's instructions, such as its bank details.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the reference the buyer quotes with a transfer, such as the order number.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets what is still owed, formatted, or <see langword="null"/>.
    /// </summary>
    public string AmountDue { get; set; }

    /// <summary>
    /// Gets or sets when it is due, or <see langword="null"/>.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets the method's name.
    /// </summary>
    public string MethodName { get; set; }

    /// <summary>
    /// Gets or sets the method's key.
    /// </summary>
    public string MethodKey { get; set; }

    /// <summary>
    /// Gets or sets the balance's transaction id, whose admin page records the payment, for the store's view.
    /// </summary>
    public string TransactionId { get; set; }
}
