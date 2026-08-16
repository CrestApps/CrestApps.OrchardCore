namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Describes card details for a payment method stored with a subscription.
/// </summary>
public sealed class PaymentCardInfo
{
    /// <summary>
    /// Gets or sets the card brand.
    /// </summary>
    public string Brand { get; set; }

    /// <summary>
    /// Gets or sets the card issuer country.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets the last four digits of the card number.
    /// </summary>
    public string LastFour { get; set; }

    /// <summary>
    /// Gets or sets the card expiration month.
    /// </summary>
    public long? ExpirationMonth { get; set; }

    /// <summary>
    /// Gets or sets the card expiration year.
    /// </summary>
    public long? ExpirationYear { get; set; }

    /// <summary>
    /// Gets or sets the card fingerprint returned by the payment gateway.
    /// </summary>
    public string Fingerprint { get; set; }

    /// <summary>
    /// Gets or sets the card issuer name.
    /// </summary>
    public string Issuer { get; set; }
}
