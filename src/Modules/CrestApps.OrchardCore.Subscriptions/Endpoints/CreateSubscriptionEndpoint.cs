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

namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

public static class CreateSubscriptionEndpoint
{
    public static IEndpointRouteBuilder AddCreateStripeSubscriptionEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/stripe/create-subscription", HandleAsync)
            .AllowAnonymous()
            .WithName(SubscriptionConstants.RouteName.CreateSubscriptionEndpoint)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSessionSubscriptionPayment model,
        IClock clock,
        ISubscriptionSessionStore subscriptionSessionStore,
        IStripeSubscriptionService stripeSubscriptionService,
        IStripePaymentMethodService stripePaymentMethodService,
        IStripePriceService stripePriceService,
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

        var session = await subscriptionSessionStore.GetAsync(model.SessionId, SubscriptionSessionStatus.Pending);

        if (session == null)
        {
            return TypedResults.NotFound();
        }

        if (!session.TryGet<Invoice>(out var invoice))
        {
            return TypedResults.NotFound();
        }

        var stripeMetadata = session.As<StripeMetadata>();

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
                var price = await stripePriceService.GetAsync(lineItem.Id);

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
                        { nameof(ContentItem.ContentItemVersionId), lineItem.Id },
                    },
                });

                subscriptionLineItems.Add(lineItem);
            }

            if (stripeCreateRequest.LineItems.Count == 0)
            {
                continue;
            }

            var result = await stripeSubscriptionService.CreateAsync(stripeCreateRequest);

            results.Add(new
            {
                id = result.Id,
                status = result.Status,
                clientSecret = result.Status == "requires_action" ? result.ClientSecret : null,
            });

            var stringSubscriptionMetadata = new StripeSubscriptionMetadata()
            {
                SubscriptionId = result.Id,
                CreatedAt = now,
                ExpiresAt = subscription.Key.Type switch
                {
                    DurationType.Day => now.AddDays(subscription.Key.Duration),
                    DurationType.Week => now.AddDays(subscription.Key.Duration * 7),
                    DurationType.Month => now.AddMonths(subscription.Key.Duration),
                    DurationType.Year => now.AddYears(subscription.Key.Duration),
                    _ => null
                },
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
