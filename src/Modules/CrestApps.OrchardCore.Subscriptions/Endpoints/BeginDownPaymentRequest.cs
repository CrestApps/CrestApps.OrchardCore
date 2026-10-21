namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

/// <summary>
/// The body of a request that starts an installment plan's down payment.
/// </summary>
public sealed class BeginDownPaymentRequest
{
    /// <summary>
    /// Gets or sets the chosen payment provider.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets where a hosted provider returns after paying.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets where a hosted provider returns after canceling.
    /// </summary>
    public string CancelUrl { get; set; }

    /// <summary>
    /// Gets or sets what the provider's script produced, such as a tokenized card.
    /// </summary>
    public Dictionary<string, string> ProviderData { get; set; }
}
