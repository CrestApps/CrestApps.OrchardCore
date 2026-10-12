namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The caller-supplied inputs for beginning payment on a checkout: which provider to use and where a hosted
/// provider should return the customer.
/// </summary>
public sealed class BeginPaymentOptions
{
    /// <summary>
    /// Gets or sets the key of the payment provider the customer chose.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the absolute URL a hosted provider returns the customer to after a successful payment.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the absolute URL a hosted provider returns the customer to when they cancel.
    /// </summary>
    public string CancelUrl { get; set; }

    /// <summary>
    /// Gets or sets values the provider's own client script collected before payment began, for example a
    /// tokenized payment method.
    /// </summary>
    /// <remarks>
    /// This exists because some providers cannot start without something only the browser can produce: a
    /// recurring agreement usually needs a reusable payment method, and tokenizing the card in the customer's
    /// browser is exactly what keeps the card details out of this application. The dictionary is opaque to the
    /// checkout — only the provider that asked for a value interprets it.
    /// </remarks>
    public IReadOnlyDictionary<string, string> ProviderData { get; set; }
}
