namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// The store's EasyPost account, under Settings › Commerce › Shipping. Both values are kept encrypted.
/// </summary>
public sealed class EasyPostSettings
{
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the secret EasyPost signs its tracking updates with.
    /// </summary>
    public string WebhookSecret { get; set; }
}
