using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Checkout.Core.Api;

/// <summary>
/// Put on every API route that changes what a checkout costs (shipping, contact, codes, balances). Before the change, a
/// payment begun for the old total is canceled at the provider and superseded, so its client secret can't settle the old
/// amount; when the provider has already taken it, or won't cancel it, the change is refused with 409.
/// </summary>
public sealed class ReleaseLivePaymentEndpointFilter : IEndpointFilter
{
    /// <summary>
    /// The route value that names the checkout.
    /// </summary>
    public const string RouteValueName = "id";

    /// <inheritdoc/>
    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Only a checkout the caller owns is touched: the lookup is the same ownership-checked one the handlers use, so a
        // stranger who knows a checkout id can't cancel its payment. Anything else is left to the handler, which answers 404.
        if (httpContext.Request.RouteValues.TryGetValue(RouteValueName, out var value) &&
            value?.ToString() is { Length: > 0 } sessionId &&
            await CheckoutApi.FindOpenAsync(httpContext.RequestServices.GetRequiredService<ICheckoutSessionStore>(), sessionId, httpContext.RequestAborted) is not null)
        {
            var releaser = httpContext.RequestServices.GetRequiredService<ICheckoutPaymentReleaseService>();

            if (await releaser.ReleaseAsync(sessionId, httpContext.RequestAborted) == CheckoutPaymentReleaseResult.PaymentInProgress)
            {
                return ApiProblems.Conflict(
                    ApiProblems.Codes.PaymentInProgress,
                    "A payment for this checkout is in progress. Wait for it to finish before changing the checkout.");
            }
        }

        return await next(context);
    }
}
