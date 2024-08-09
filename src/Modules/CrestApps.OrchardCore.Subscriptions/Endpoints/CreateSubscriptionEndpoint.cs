using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

public static class CreateSubscriptionEndpoint
{
    public static IEndpointRouteBuilder AddCreateStripeSubscriptionEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/stripe/create-subscription", HandleAsync)
            .AllowAnonymous()
            .WithName(SubscriptionsConstants.RouteName.CreateSubscriptionEndpoint)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSessionSubscriptionPayment model,
        ISubscriptionSessionStore subscriptionSessionStore,
        IStripeSubscriptionService stripeSubscriptionService,
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

        var request = new CreateSubscriptionRequest()
        {
            PaymentMethodId = model.PaymentMethodId,
            CustomerId = model.CustomerId,
            PlanId = session.ContentItemVersionId,
            Metadata = model.Metadata,
        };

        var response = await stripeSubscriptionService.CreateAsync(request);

        if (response.Status == "requires_action")
        {
            return TypedResults.Ok(new
            {
                status = "requires_action",
                clientSecret = response.ClientSecret
            });
        }

        return TypedResults.Ok(new
        {
            status = response.Status,
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
