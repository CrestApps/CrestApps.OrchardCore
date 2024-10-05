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

        var now = clock.UtcNow;
        var results = new List<object>();
        stripeMetadata.Subscriptions ??= [];

        foreach (var subscription in subscriptionGroups)
        {
            var request = new CreateSubscriptionRequest
            {
                PaymentMethodId = model.PaymentMethodId,
                CustomerId = model.CustomerId,
                LineItems = [],
                Metadata = model.Metadata ?? [],
                BillingCycles = invoice.BillingCycles,
            };

            request.Metadata["sessionId"] = model.SessionId;

            foreach (var lineItem in subscription.Value)
            {
                var price = await stripePriceService.GetAsync(lineItem.Id);

                if (price == null)
                {
                    continue;
                }

                request.LineItems.Add(new SubscriptionLineItem()
                {
                    Quantity = lineItem.Quantity,
                    PriceId = price.Id,
                    Metadata = new Dictionary<string, string>()
                    {
                        { nameof(ContentItem.ContentItemVersionId), lineItem.Id },
                    },
                });

                var result = await stripeSubscriptionService.CreateAsync(request);

                results.Add(new
                {
                    id = result.Id,
                    status = result.Status,
                    clientSecret = result.Status == "requires_action" ? result.ClientSecret : null,
                });

                stripeMetadata.Subscriptions[result.Id] = new StripeSubscriptionMetadata()
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
            }
        }

        session.Put(stripeMetadata);
        session.Put(new SubscriptionCollectionMetadata()
        {
            Subscriptions = stripeMetadata.Subscriptions.Values.Select(x => new SubscriptionMetadata()
            {
                ExpiresAt = x.ExpiresAt,
                StartedAt = x.CreatedAt,
                SubscriptionId = x.SubscriptionId,
                Gateway = StripeConstants.ProcessorKey,
                GatewayMode = stripeOptions.Value.IsLive ? Payments.GatewayMode.Live : Payments.GatewayMode.Testing,
                GatewayCustomerId = model.CustomerId,
            }).ToArray(),
        });

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
