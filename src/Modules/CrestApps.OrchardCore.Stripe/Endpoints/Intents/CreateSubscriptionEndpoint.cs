using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Endpoints.Intents;

public static class CreateSubscriptionEndpoint
{
    public static IEndpointRouteBuilder AddCreateSubscriptionEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("stripe/create-subscription", HandleAsync)
            .AllowAnonymous()
            .WithName(StripeConstants.RouteName.CreateSubscriptionEndpoint)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSubscriptionRequest model,
        IOptions<StripeOptions> stripeOptions)
    {
        if (!IsValid(model))
        {
            return TypedResults.BadRequest(new
            {
                ErrorMessage = "Invalid request data",
                ErrorCode = 1,
            });
        }

        var subscriptionOptions = new SubscriptionCreateOptions
        {
            Customer = model.CustomerId,
            Items =
            [
                new() {
                    Plan = model.PlanId,
                },
            ],
            DefaultPaymentMethod = model.PaymentMethodId,
            Expand = ["latest_invoice.payment_intent"],
        };

        var stripeClient = new StripeClient(stripeOptions.Value.ApiKey);
        var subscriptionService = new SubscriptionService(stripeClient);
        var subscription = await subscriptionService.CreateAsync(subscriptionOptions);

        if (subscription.LatestInvoice.PaymentIntent.Status == "requires_action")
        {
            return TypedResults.Ok(new
            {
                status = "requires_action",
                client_secret = subscription.LatestInvoice.PaymentIntent.ClientSecret
            });
        }

        return TypedResults.Ok(new
        {
            status = subscription.Status
        });
    }

    private static bool IsValid(CreateSubscriptionRequest model)
    {
        return
            !string.IsNullOrWhiteSpace(model.CustomerId) &&
            !string.IsNullOrWhiteSpace(model.PlanId) &&
            !string.IsNullOrWhiteSpace(model.PaymentMethodId);
    }
}
