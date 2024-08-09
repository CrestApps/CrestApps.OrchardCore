using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Stripe.Endpoints;

public static class CreateSetupIntentEndpoint
{
    public static IEndpointRouteBuilder AddCreateSetupIntentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("stripe/create-setup-intent", HandleAsync)
            .AllowAnonymous()
            .WithName(StripeConstants.RouteName.CreateSetupIntentEndpoint)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSetupIntentRequest model,
        IOptions<StripeOptions> stripeOptions,
        IStripeSetupIntentService stripeSetupIntentService)
    {
        if (string.IsNullOrEmpty(stripeOptions.Value.ApiKey))
        {
            return TypedResults.Problem("Stripe is not configured.", instance: null, statusCode: 500);
        }

        if (string.IsNullOrWhiteSpace(model.PaymentMethodId))
        {
            return TypedResults.BadRequest(new
            {
                ErrorMessage = "Invalid request data",
                ErrorCode = 1,
            });
        }

        var request = new CreateSetupIntentRequest
        {
            PaymentMethodId = model.PaymentMethodId,
            Metadata = model.Metadata,
        };

        var result = await stripeSetupIntentService.CreateAsync(request);

        return TypedResults.Ok(new
        {
            clientSecret = result.ClientSecret,
            customerId = result.CustomerId,
            status = result.Status,
        });
    }
}
