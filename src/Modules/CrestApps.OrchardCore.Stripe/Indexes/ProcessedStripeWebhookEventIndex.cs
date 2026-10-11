using YesSql.Indexes;

namespace CrestApps.OrchardCore.Stripe.Indexes;

/// <summary>
/// Indexes <see cref="Models.ProcessedStripeWebhookEvent"/> by its Stripe event id so processed
/// events can be looked up cheaply for de-duplication.
/// </summary>
public sealed class ProcessedStripeWebhookEventIndex : MapIndex
{
    public string EventId { get; set; }

    public DateTime ProcessedUtc { get; set; }
}
