using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A device or browser a worker turned push notifications on for.
/// </summary>
public sealed class WebPushSubscription : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the push service endpoint, unique per device.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the device's public key.
    /// </summary>
    public string P256dh { get; set; }

    /// <summary>
    /// Gets or sets the device's authentication secret.
    /// </summary>
    public string Auth { get; set; }

    /// <summary>
    /// Gets or sets the browser it came from, to show the worker.
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time a push last reached it.
    /// </summary>
    public DateTime? LastSentUtc { get; set; }
}
