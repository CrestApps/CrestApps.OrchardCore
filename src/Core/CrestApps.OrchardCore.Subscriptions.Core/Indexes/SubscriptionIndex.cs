using CrestApps.OrchardCore.Payments;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

/// <summary>
/// Indexes completed subscription records stored in a subscription session.
/// </summary>
public sealed class SubscriptionIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the UTC time at which the subscription started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the subscription expires, when the subscription has a known expiration.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the payment gateway or processor key that owns the subscription.
    /// </summary>
    public string Gateway { get; set; }

    /// <summary>
    /// Gets or sets the environment mode used by the payment gateway for the subscription.
    /// </summary>
    public GatewayMode GatewayMode { get; set; }

    /// <summary>
    /// Gets or sets the content type of the subscription content item that was purchased.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the subscription content item that was purchased.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the version identifier of the subscription content item that was purchased.
    /// </summary>
    public string ContentItemVersionId { get; set; }

    /// <summary>
    /// Gets or sets the subscription session identifier that produced this subscription.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user that owns the subscription session.
    /// </summary>
    public string OwnerId { get; set; }
}
