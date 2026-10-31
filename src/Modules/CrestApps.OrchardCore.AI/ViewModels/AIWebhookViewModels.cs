using CrestApps.OrchardCore.AI.Core.Webhooks;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// The editor of a webhook subscription.
/// </summary>
public class AIWebhookSubscriptionViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the HTTPS address.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether events are sent.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the events sent.
    /// </summary>
    public string[] Events { get; set; } = [];

    /// <summary>
    /// Gets or sets a new signing secret; empty keeps the saved one.
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a secret is saved.
    /// </summary>
    public bool HasSecret { get; set; }
}

/// <summary>
/// The delivery log of a webhook subscription.
/// </summary>
public class AIWebhookDeliveriesViewModel
{
    /// <summary>
    /// Gets or sets the subscription.
    /// </summary>
    public AIWebhookSubscription Subscription { get; set; }

    /// <summary>
    /// Gets or sets the deliveries for the admin list.
    /// </summary>
    public AdminListContext List { get; set; }
}
