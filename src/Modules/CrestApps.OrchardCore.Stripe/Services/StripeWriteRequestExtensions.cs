using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

internal static class StripeWriteRequestExtensions
{
    /// <summary>
    /// Converts the idempotency key on a <see cref="StripeWriteRequest"/> into Stripe <see cref="RequestOptions"/>.
    /// Returns <c>null</c> when no key is set so the SDK behaves exactly as before.
    /// </summary>
    /// <summary>
    /// Builds request options whose idempotency key is derived from the request's own, for a resource created
    /// alongside the main one. Reusing the main key verbatim would make Stripe return the main resource.
    /// </summary>
    public static RequestOptions ToRequestOptions(this StripeWriteRequest request, string suffix)
    {
        var options = request.ToRequestOptions();

        if (!string.IsNullOrEmpty(options?.IdempotencyKey))
        {
            options.IdempotencyKey += suffix;
        }

        return options;
    }

    public static RequestOptions ToRequestOptions(this StripeWriteRequest request)
        => request is null || string.IsNullOrEmpty(request.IdempotencyKey)
            ? null
            : new RequestOptions { IdempotencyKey = request.IdempotencyKey };
}
