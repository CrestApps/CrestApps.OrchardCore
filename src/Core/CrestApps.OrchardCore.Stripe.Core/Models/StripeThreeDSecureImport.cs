namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// The result of a 3-D Secure authentication run outside Stripe, which Stripe imports with a payment
/// (<c>payment_method_options[card][three_d_secure]</c>) instead of running its own. Each value must match what the
/// 3-D Secure provider returned.
/// </summary>
public sealed class StripeThreeDSecureImport
{
    /// <summary>
    /// Gets or sets the authentication value (CAVV, AAV or AEVV): 20 bytes, base64-encoded into 28 characters.
    /// </summary>
    public string Cryptogram { get; set; }

    /// <summary>
    /// Gets or sets the electronic commerce indicator: <c>01</c>, <c>02</c>, <c>05</c>, <c>06</c> or <c>07</c>.
    /// </summary>
    public string ElectronicCommerceIndicator { get; set; }

    /// <summary>
    /// Gets or sets the directory server transaction id (<c>dsTransID</c>), or the XID of 3-D Secure 1.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the 3-D Secure version that ran, such as <c>2.2.0</c>.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the <c>transStatus</c> the issuer returned in the ARes, when it is known.
    /// </summary>
    public string AresTransStatus { get; set; }
}
