using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Stripe.Endpoints;

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

        var response = await stripeSubscriptionService.CreateAsync(model);

        if (response.Status == "requires_action")
        {
            return TypedResults.Ok(new
            {
                id = response.Id,
                status = "requires_action",
                clientSecret = response.ClientSecret
            });
        }

        return TypedResults.Ok(new
        {
            id = response.Id,
            status = response.Status,
        });
    }

    private static bool IsValid(CreateSubscriptionRequest model)
    {
        return
            !string.IsNullOrWhiteSpace(model.CustomerId) &&
            model.LineItems != null &&
            model.LineItems.All(x => !string.IsNullOrEmpty(x.PriceId) && x.Quantity > 0) &&
            !string.IsNullOrWhiteSpace(model.PaymentMethodId);
    }
}
