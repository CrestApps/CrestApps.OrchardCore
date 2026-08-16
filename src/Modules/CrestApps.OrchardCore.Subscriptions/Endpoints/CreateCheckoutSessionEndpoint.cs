using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

/// <summary>
/// Registers the Stripe checkout session creation endpoint for subscription payments.
/// </summary>
public static class CreateCheckoutSessionEndpoint
{
    /// <summary>
    /// Adds the endpoint that creates a Stripe checkout session for a pending subscription session.
    /// </summary>
    /// <param name="builder">The endpoint route builder used to register the route.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder AddCreateCheckoutSessionEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/stripe/create-checkout-session", HandleAsync)
            .AllowAnonymous()
            .WithName(SubscriptionConstants.RouteName.CreateCheckoutSessionEndpoint)
            .DisableAntiforgery()
            .WithMetadata(new RateLimitGroupAttribute(SubscriptionConstants.RateLimitGroups.Payment));

        return builder;
    }

    /// <summary>
    /// Handles a request to create a Stripe checkout session for subscription line items.
    /// </summary>
    /// <param name="model">The checkout session creation request.</param>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="linkGenerator">The link generator used to build checkout return URLs.</param>
    /// <param name="subscriptionSessionStore">The store used to load pending subscription sessions.</param>
    /// <param name="stripeCheckoutService">The Stripe checkout service used to create checkout sessions.</param>
    /// <param name="stripePriceService">The Stripe price service used to resolve line item prices.</param>
    /// <param name="paymentAttemptLimiter">The payment attempt limiter used to throttle repeated requests.</param>
    /// <param name="stripeOptions">The configured Stripe options.</param>
    /// <returns>An HTTP result that contains the checkout URL or an error response.</returns>
    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSessionCheckout model,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        ISubscriptionSessionStore subscriptionSessionStore,
        IStripeCheckoutService stripeCheckoutService,
        IStripePriceService stripePriceService,
        IPaymentAttemptLimiter paymentAttemptLimiter,
        IOptions<StripeOptions> stripeOptions)
    {
        if (string.IsNullOrEmpty(stripeOptions.Value.ApiKey))
        {
            return TypedResults.Problem("Stripe is not configured.", instance: null, statusCode: 500);
        }

        if (model == null || string.IsNullOrWhiteSpace(model.SessionId))
        {
            return TypedResults.BadRequest(new { ErrorMessage = "Invalid request data", ErrorCode = 1 });
        }

        if (!await PaymentEndpointThrottle.AllowAsync(paymentAttemptLimiter, httpContext, "checkout-session", model.SessionId))
        {
            return PaymentEndpointThrottle.TooManyRequests();
        }

        var session = await subscriptionSessionStore.GetAsync(model.SessionId, SubscriptionSessionStatus.Pending);

        if (session == null)
        {
            return TypedResults.NotFound();
        }

        if (!session.TryGet<Invoice>(out var invoice))
        {
            return TypedResults.NotFound();
        }

        if (!StripeCheckoutRequestFactory.IsEligible(invoice, out var reason))
        {
            return TypedResults.BadRequest(new { ErrorMessage = reason, ErrorCode = 2 });
        }

        var lineItems = new List<CreateCheckoutLineItem>();

        foreach (var group in invoice.GetSubscriptionGroups())
        {
            foreach (var lineItem in group.Value)
            {
                var price = await stripePriceService.GetAsync(lineItem.Id);

                if (price == null)
                {
                    continue;
                }

                lineItems.Add(new CreateCheckoutLineItem
                {
                    PriceId = price.Id,
                    Quantity = lineItem.Quantity,
                });
            }
        }

        if (lineItems.Count == 0)
        {
            return TypedResults.BadRequest(new { ErrorMessage = "None of the subscription line items could be matched to a Stripe price.", ErrorCode = 3 });
        }

        var controllerName = typeof(SubscriptionsController).ControllerName();

        var successUrl = linkGenerator.GetUriByAction(
            httpContext,
            action: nameof(SubscriptionsController.CheckoutReturn),
            controller: controllerName,
            values: new { area = SubscriptionConstants.Features.Area, sessionId = model.SessionId });

        var cancelUrl = linkGenerator.GetUriByAction(
            httpContext,
            action: nameof(SubscriptionsController.Display),
            controller: controllerName,
            values: new { area = SubscriptionConstants.Features.Area, sessionId = model.SessionId, step = SubscriptionConstants.StepKey.Payment });

        if (string.IsNullOrEmpty(successUrl) || string.IsNullOrEmpty(cancelUrl))
        {
            return TypedResults.Problem("Unable to build the checkout return URLs.", instance: null, statusCode: 500);
        }

        // Stripe substitutes the '{CHECKOUT_SESSION_ID}' template token with the created session id. The
        // token must be appended verbatim (not URL-encoded) so Stripe can find and replace it.
        successUrl += (successUrl.Contains('?') ? "&" : "?") + "checkoutSessionId={CHECKOUT_SESSION_ID}";

        var request = StripeCheckoutRequestFactory.Create(
            sessionId: model.SessionId,
            lineItems: lineItems,
            successUrl: successUrl,
            cancelUrl: cancelUrl);

        // Bind the key to the session and its line items so a retried checkout-session creation reuses
        // the same Stripe session instead of creating duplicates.
        request.IdempotencyKey = StripeIdempotencyKey.Compute(
            "sub_cs",
            model.SessionId,
            string.Join(',', lineItems.Select(x => $"{x.PriceId}:{x.Quantity}")));

        var response = await stripeCheckoutService.CreateAsync(request);

        if (string.IsNullOrEmpty(response?.Url))
        {
            return TypedResults.Problem("Stripe did not return a checkout URL.", instance: null, statusCode: 500);
        }

        return TypedResults.Ok(new { url = response.Url });
    }
}
