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

        var invoice = session.As<Invoice>();

        if (invoice == null)
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

        var request = new CreateSubscriptionRequest()
        {
            PaymentMethodId = model.PaymentMethodId,
            CustomerId = model.CustomerId,
            LineItems = [],
            Metadata = model.Metadata ?? [],
        };

        foreach (var lineItem in invoice.LineItems)
        {
            if (lineItem.Subscription == null)
            {
                // At this point, this isn't a subscription line item. Ignore it.
                continue;
            }

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
        }

        request.BillingCycles = invoice.BillingCycles;
        request.Metadata["sessionId"] = model.SessionId;

        var result = await stripeSubscriptionService.CreateAsync(request);

        stripeMetadata.SubscriptionId = result.Id;

        session.Put(stripeMetadata);

        await subscriptionSessionStore.SaveAsync(session);

        if (result.Status == "requires_action")
        {
            return TypedResults.Ok(new
            {
                id = result.Id,
                status = "requires_action",
                clientSecret = result.ClientSecret
            });
        }

        return TypedResults.Ok(new
        {
            id = result.Id,
            status = result.Status,
        });
    }

    private static bool IsValid(CreateSessionSubscriptionPayment model)
    {
        return
            !string.IsNullOrWhiteSpace(model.CustomerId) &&
            !string.IsNullOrWhiteSpace(model.SessionId) &&
            !string.IsNullOrWhiteSpace(model.PaymentMethodId);
    }
}
