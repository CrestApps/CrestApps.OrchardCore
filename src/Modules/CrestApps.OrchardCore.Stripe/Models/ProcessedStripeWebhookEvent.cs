namespace CrestApps.OrchardCore.Stripe.Models;

/// <summary>
/// A durable record that a specific Stripe webhook event has already been processed successfully.
/// Persisted so that Stripe's at-least-once delivery (duplicate or replayed events) is de-duplicated
/// and the payment/subscription side effects run exactly once.
/// </summary>
public sealed class ProcessedStripeWebhookEvent
{
    /// <summary>
    /// The Stripe event identifier (e.g. <c>evt_...</c>). Globally unique per event.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// The Stripe event type (e.g. <c>invoice.payment_succeeded</c>).
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// The UTC time the event was processed.
    /// </summary>
    public DateTime ProcessedUtc { get; set; }
}
