using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

internal static class StripeWriteRequestExtensions
{
    /// <summary>
    /// Converts the idempotency key on a <see cref="StripeWriteRequest"/> into Stripe <see cref="RequestOptions"/>.
    /// Returns <c>null</c> when no key is set so the SDK behaves exactly as before.
    /// </summary>
    public static RequestOptions ToRequestOptions(this StripeWriteRequest request)
        => request is null || string.IsNullOrEmpty(request.IdempotencyKey)
            ? null
            : new RequestOptions { IdempotencyKey = request.IdempotencyKey };
}
