namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// The store's EasyPost account, on the EasyPost tab of Settings › Commerce › Shipping. The API key and the webhook
/// secret are kept encrypted.
/// </summary>
public sealed class EasyPostSettings
{
    /// <summary>
    /// Gets or sets whether the store uses EasyPost as a carrier provider.
    /// </summary>
    public bool IsEnabled { get; set; }

    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the secret EasyPost signs its tracking updates with. The store never enters it: it's made when the
    /// webhook is registered with EasyPost.
    /// </summary>
    public string WebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets EasyPost's id for the webhook registered for this site, or <see langword="null"/>.
    /// </summary>
    public string WebhookId { get; set; }

    /// <summary>
    /// Gets or sets the address the webhook was registered for.
    /// </summary>
    public string WebhookUrl { get; set; }
}
