using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Models;
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
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

/// <summary>
/// Registers the Stripe subscription creation endpoint for subscription sessions.
/// </summary>
public static class CreateSubscriptionEndpoint{

    /// <summary>
    /// Adds the endpoint that creates Stripe subscriptions for a pending subscription session.
    /// </summary>
    /// <param name="builder">The endpoint route builder used to register the route.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder AddCreateStripeSubscriptionEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/stripe/create-subscription", HandleAsync)
            .AllowAnonymous()
            .WithName(SubscriptionConstants.RouteName.CreateSubscriptionEndpoint)
            .DisableAntiforgery()
            .WithMetadata(new RateLimitGroupAttribute(SubscriptionConstants.RateLimitGroups.Payment));

        return builder;
    }

    /// <summary>
    /// Handles a request to create one or more Stripe subscriptions for grouped subscription line items.
    /// </summary>
    /// <param name="model">The subscription creation request.</param>
    /// <param name="clock">The clock used to timestamp created subscriptions.</param>
    /// <param name="subscriptionSessionStore">The store used to load and save subscription sessions.</param>
    /// <param name="stripeSubscriptionService">The Stripe subscription service used to create subscriptions.</param>
    /// <param name="stripePaymentMethodService">The Stripe payment method service used to read payment card details.</param>
    /// <param name="stripePriceService">The Stripe price service used to resolve line item prices.</param>
    /// <param name="paymentAttemptLimiter">The payment attempt limiter used to throttle repeated requests.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to read the current request.</param>
    /// <param name="stripeOptions">The configured Stripe options.</param>
    /// <returns>An HTTP result that contains subscription creation results or an error response.</returns>
    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSessionSubscriptionPayment model,
        IClock clock,
        ISubscriptionSessionStore subscriptionSessionStore,
        IStripeSubscriptionService stripeSubscriptionService,
        IStripePaymentMethodService stripePaymentMethodService,
        IStripePriceService stripePriceService,
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

        if (!await PaymentEndpointThrottle.AllowAsync(paymentAttemptLimiter, httpContextAccessor.HttpContext, "subscription", model.SessionId))
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

        // Group line items by subscription duration, ensuring that each subscription has a single, unified expiration date.
        var subscriptionGroups = invoice.GetSubscriptionGroups();

        var cardInfo = await stripePaymentMethodService.GetInformationAsync(model.PaymentMethodId);

        var subscriptionMetadata = new SubscriptionsMetadata()
        {
            Subscriptions = [],
        };

        var now = clock.UtcNow;
        var results = new List<object>();
        stripeMetadata.Subscriptions ??= [];

        foreach (var subscription in subscriptionGroups)
        {
            var subscriptionInfo = new SubscriptionInfo()
            {
                LineItems = [],
            };

            var stripeCreateRequest = new CreateSubscriptionRequest
            {
                PaymentMethodId = model.PaymentMethodId,
                CustomerId = model.CustomerId,
                LineItems = [],
                Metadata = model.Metadata ?? [],
                BillingCycles = invoice.BillingCycles,
            };

            var subscriptionLineItems = new List<InvoiceLineItem>();

            stripeCreateRequest.Metadata["sessionId"] = model.SessionId;

            foreach (var lineItem in subscription.Value)
            {
                var price = await stripePriceService.GetAsync(lineItem.ItemId);

                if (price == null)
                {
                    continue;
                }

                subscriptionInfo.LineItems.Add(lineItem);
                stripeCreateRequest.LineItems.Add(new CreateSubscriptionLineItem()
                {
                    Quantity = lineItem.Quantity,
                    PriceId = price.Id,
                    Metadata = new Dictionary<string, string>()
                    {
                        { nameof(ContentItem.ContentItemVersionId), lineItem.ItemId },
                    },
                });

                subscriptionLineItems.Add(lineItem);
            }

            if (stripeCreateRequest.LineItems.Count == 0)
            {
                continue;
            }

            // Bind the key to the session, customer, payment method and the exact set of priced line
            // items so retries of this subscription creation never produce duplicates, while distinct
            // subscription groups within the same session each get their own key.
            stripeCreateRequest.IdempotencyKey = StripeIdempotencyKey.Compute(
                "sub_sub",
                model.SessionId,
                model.CustomerId,
                model.PaymentMethodId,
                $"{subscription.Key.Type}:{subscription.Key.Duration}",
                string.Join(',', stripeCreateRequest.LineItems.Select(x => $"{x.PriceId}:{x.Quantity}")));

            var result = await stripeSubscriptionService.CreateAsync(stripeCreateRequest);

            results.Add(new
            {
                id = result.Id,
                status = result.RequiresAction ? "requires_action" : result.Status,
                clientSecret = result.RequiresAction ? result.ClientSecret : null,
            });

            var stringSubscriptionMetadata = new StripeSubscriptionMetadata()
            {
                SubscriptionId = result.Id,
                CreatedAt = now,
                ExpiresAt = BillingSchedule.GetNextBillingDate(now, subscription.Key.Type, subscription.Key.Duration),
            };

            stripeMetadata.Subscriptions[result.Id] = stringSubscriptionMetadata;

            subscriptionInfo.ExpiresAt = stringSubscriptionMetadata.ExpiresAt;
            subscriptionInfo.StartedAt = stringSubscriptionMetadata.CreatedAt;
            subscriptionInfo.SubscriptionId = stringSubscriptionMetadata.SubscriptionId;
            subscriptionInfo.Gateway = StripeConstants.ProcessorKey;
            subscriptionInfo.GatewayMode = stripeOptions.Value.IsLive ? GatewayMode.Live : GatewayMode.Testing;
            subscriptionInfo.GatewayCustomerId = model.CustomerId;

            if (cardInfo?.Card != null)
            {
                subscriptionInfo.PaymentMethod = new PaymentMethodInfo
                {
                    Card = new PaymentCardInfo
                    {
                        LastFour = cardInfo.Card.LastFour,
                        Brand = cardInfo.Card.Brand,
                        Country = cardInfo.Card.Country,
                        ExpirationMonth = cardInfo.Card.ExpirationMonth,
                        ExpirationYear = cardInfo.Card.ExpirationYear,
                        Fingerprint = cardInfo.Card.Fingerprint,
                        Issuer = cardInfo.Card.Issuer,
                    },
                };
            }

            subscriptionMetadata.Subscriptions.Add(subscriptionInfo);
        }

        session.Put(stripeMetadata);
        session.Put(subscriptionMetadata);

        await subscriptionSessionStore.SaveAsync(session);

        return TypedResults.Ok(results);
    }

    private static bool IsValid(CreateSessionSubscriptionPayment model)
    {
        return
            !string.IsNullOrWhiteSpace(model.CustomerId) &&
            !string.IsNullOrWhiteSpace(model.SessionId) &&
            !string.IsNullOrWhiteSpace(model.PaymentMethodId);
    }
}
