namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Base type for Stripe write requests that support an idempotency key. Supplying a stable key makes
/// the create/confirm operation safe to retry: Stripe returns the original result instead of creating
/// a duplicate object (e.g. a second charge) when the same key is replayed.
/// </summary>
public abstract class StripeWriteRequest
{
    /// <summary>
    /// An optional idempotency key. When set, it is forwarded to Stripe so retried requests with the
    /// same key and parameters do not produce duplicate side effects.
    /// </summary>
    public string IdempotencyKey { get; set; }
}
