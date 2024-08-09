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

namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

public static class CreatePaymentIntentEndpoint
{
    public static IEndpointRouteBuilder AddCreatePaymentIntentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/stripe/create-payment-intent", HandleAsync)
            .AllowAnonymous()
            .WithName(SubscriptionsConstants.RouteName.CreatePaymentIntentEndpoint)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSessionPaymentIntent model,
        ISubscriptionSessionStore subscriptionSessionStore,
        IStripePaymentService stripePaymentService,
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

        var request = new CreatePaymentIntentRequest()
        {
            PaymentMethodId = model.PaymentMethodId,
            CustomerId = model.CustomerId,
            Metadata = model.Metadata,
            Amount = invoice.DueNow,
            Currency = "USD",
        };

        var result = await stripePaymentService.CreateAsync(request);

        return TypedResults.Ok(new
        {
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
}
