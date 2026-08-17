using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

/// <summary>
/// Registers the Stripe payment intent creation endpoint for initial subscription payments.
/// </summary>
public static class CreatePaymentIntentEndpoint
{
    /// <summary>
    /// Adds the endpoint that creates a Stripe payment intent for an initial subscription payment.
    /// </summary>
    /// <param name="builder">The endpoint route builder used to register the route.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder AddCreatePaymentIntentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/stripe/create-payment-intent", HandleAsync)
            .AllowAnonymous()
            .WithName(SubscriptionConstants.RouteName.CreatePaymentIntentEndpoint)
            .DisableAntiforgery()
            .WithMetadata(new RateLimitGroupAttribute(SubscriptionConstants.RateLimitGroups.Payment));

        return builder;
    }

    /// <summary>
    /// Handles a request to create a Stripe payment intent for the initial amount due on a subscription session.
    /// </summary>
    /// <param name="model">The payment intent creation request.</param>
    /// <param name="subscriptionSessionStore">The store used to load and save subscription sessions.</param>
    /// <param name="stripePaymentService">The Stripe payment intent service used to create payment intents.</param>
    /// <param name="paymentAttemptLimiter">The payment attempt limiter used to throttle repeated requests.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to read the current request.</param>
    /// <param name="stripeOptions">The configured Stripe options.</param>
    /// <returns>An HTTP result that contains payment intent details or an error response.</returns>
    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSessionPaymentIntent model,
        ISubscriptionSessionStore subscriptionSessionStore,
        IStripePaymentIntentService stripePaymentService,
        IPaymentAttemptLimiter paymentAttemptLimiter,
        IHttpContextAccessor httpContextAccessor,
        IOptions<StripeOptions> stripeOptions)
    {
        if (string.IsNullOrEmpty(stripeOptions.Value.ApiKey))
        {
            return TypedResults.Problem("Stripe is not configured.", instance: null, statusCode: 500);
        }

        if (!IsValid(model))
        {
            return TypedResults.BadRequest(new
            {
                ErrorMessage = "Invalid request data",
                ErrorCode = 1,
            });
        }

        if (!await PaymentEndpointThrottle.AllowAsync(paymentAttemptLimiter, httpContextAccessor.HttpContext, "payment-intent", model.SessionId))
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

        var stripeMetadata = session.GetOrCreate<StripeMetadata>();

        if (stripeMetadata.CustomerId != model.CustomerId ||
            stripeMetadata.PaymentMethodId != model.PaymentMethodId)
        {
            return TypedResults.BadRequest(new
            {
                ErrorMessage = "Invalid request data",
                ErrorCode = 2,
            });
        }

        if (invoice.InitialPaymentAmount is null ||
            invoice.InitialPaymentAmount < GetMinimumAllowed(invoice.Currency))
        {
            return TypedResults.BadRequest(new
            {
                ErrorMessage = "No initial payment is required.",
                ErrorCode = 3,
            });
        }

        var request = new CreatePaymentIntentRequest()
        {
            PaymentMethodId = model.PaymentMethodId,
            CustomerId = model.CustomerId,
            Metadata = model.Metadata ?? [],
            Amount = invoice.InitialPaymentAmount ?? 0,
            Currency = invoice.Currency,
            // Bind the idempotency key to the session and the exact charge parameters. A network retry
            // or double submit collapses into one charge; a genuine re-attempt with a different payment
            // method or amount produces a new key.
            IdempotencyKey = StripeIdempotencyKey.Compute(
                "sub_pi",
                model.SessionId,
                model.CustomerId,
                model.PaymentMethodId,
                invoice.InitialPaymentAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                invoice.Currency),
        };

        request.Metadata["sessionId"] = model.SessionId;

        var result = await stripePaymentService.CreateAsync(request);

        stripeMetadata.PaymentIntentId = result.Id;
        session.Put(stripeMetadata);
        await subscriptionSessionStore.SaveAsync(session);

        return TypedResults.Ok(new
        {
            result.Id,
            clientSecret = result.ClientSecret,
            customerId = result.CustomerId,
            status = result.Status,
        });
    }

    private static bool IsValid(CreateSessionPaymentIntent model)
    {
        return
            !string.IsNullOrWhiteSpace(model.CustomerId) &&
            !string.IsNullOrWhiteSpace(model.SessionId) &&
            !string.IsNullOrWhiteSpace(model.PaymentMethodId) &&
            !string.IsNullOrWhiteSpace(model.CustomerId);
    }

    private static decimal GetMinimumAllowed(string currency)
    {
        if (StripeLimits.TryGetStripePaymentLimit(currency, out var limits))
        {
            return limits?.Minimum ?? 0;
        }

        return 0;
    }
}
