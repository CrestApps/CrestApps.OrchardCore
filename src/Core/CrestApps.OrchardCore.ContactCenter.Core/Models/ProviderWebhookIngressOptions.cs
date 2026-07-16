namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Configures tenant-local provider webhook rate and concurrency limits.
/// </summary>
public sealed class ProviderWebhookIngressOptions
{
    /// <summary>
    /// Gets or sets the maximum number of provider webhook requests that may concurrently buffer or process.
    /// </summary>
    public int ConcurrencyPermitLimit { get; set; } = 8;

    /// <summary>
    /// Gets or sets the number of authenticated webhook deliveries permitted per provider during each rate period.
    /// </summary>
    public int RatePermitLimit { get; set; } = 120;

    /// <summary>
    /// Gets or sets the authenticated provider rate-limit replenishment period in seconds.
    /// </summary>
    public int RatePeriodSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum accepted age of a provider-signed event timestamp in seconds.
    /// </summary>
    public int MaximumDeliveryAgeSeconds { get; set; } = 900;

    /// <summary>
    /// Gets or sets the maximum accepted future clock skew of a provider-signed event timestamp in seconds.
    /// </summary>
    public int MaximumFutureSkewSeconds { get; set; } = 120;
}
