using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

/// <summary>
/// Shared throttling helper for the anonymous Stripe payment endpoints. Combines the caller IP and
/// the subscription session id so automated abuse (for example card testing) is rate limited per
/// caller/session, while legitimate multi-step checkout flows are not affected.
/// </summary>
internal static class PaymentEndpointThrottle
{
    public static async Task<bool> AllowAsync(
        IPaymentAttemptLimiter limiter,
        HttpContext httpContext,
        string scope,
        string sessionId)
    {
        var ip = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        var discriminator = $"{ip}:{sessionId}";

        return await limiter.AcquireAsync(scope, discriminator);
    }

    /// <summary>
    /// A 429 response with a JSON body. The client scripts always read the response as JSON, so an
    /// empty body would throw during parsing and silently stall the checkout. The payload exposes both
    /// the <c>error</c> and <c>ErrorMessage</c> shapes used by the different payment views.
    /// </summary>
    public static IResult TooManyRequests()
    {
        const string message = "Too many payment attempts. Please wait a moment and try again.";

        return TypedResults.Json(
            new
            {
                error = message,
                ErrorMessage = message,
                ErrorCode = StatusCodes.Status429TooManyRequests,
            },
            statusCode: StatusCodes.Status429TooManyRequests);
    }
}
