using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Stripe;

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

        var paymentIntentOptions = new PaymentIntentCreateOptions
        {
            Amount = (int)(model.Amount * 100), // Amount in cents (e.g., 1000 equals $10.00)
            Currency = model.Currency,
            PaymentMethod = model.PaymentMethodId,
            ConfirmationMethod = "manual",
            Confirm = true,
            SetupFutureUsage = "off_session",
            Metadata = model.Metadata,
        };

        var stripeClient = new StripeClient(stripeOptions.Value.ApiKey);
        var paymentIntentService = new PaymentIntentService(stripeClient);
        var paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions);

        return TypedResults.Ok(new
        {
            client_secret = paymentIntent.ClientSecret,
            customer_id = paymentIntent.CustomerId
        });
    }

    private static bool IsValid(CreatePaymentIntentRequest model)
    {
        return model.Amount.HasValue &&
            !string.IsNullOrEmpty(model.Currency) &&
            model.Currency.Length == 3 &&
            !string.IsNullOrWhiteSpace(model.PaymentMethodId);
    }
}
