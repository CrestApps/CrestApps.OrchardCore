using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Stripe.Endpoints;

public static class CreatePaymentIntentEndpoint
{
    public static IEndpointRouteBuilder AddCreatePaymentIntentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("stripe/create-payment-intent", HandleAsync)
            .AllowAnonymous()
            .WithName(StripeConstants.RouteName.CreatePaymentIntentEndpoint)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreatePaymentIntentRequest model,
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

        var paymentIntent = await stripePaymentService.CreateAsync(model);

        return TypedResults.Ok(new
        {
            clientSecret = paymentIntent.ClientSecret,
            customerId = paymentIntent.CustomerId,
            status = paymentIntent.Status,
        });
    }

    private static bool IsValid(CreatePaymentIntentRequest model)
    {
        return model.Amount.HasValue &&
            !string.IsNullOrEmpty(model.Currency) &&
            model.Currency.Length == 3 &&
            !string.IsNullOrWhiteSpace(model.PaymentMethodId) &&
            !string.IsNullOrWhiteSpace(model.CustomerId);
    }
}
