using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Stripe.Endpoints;

public static class CreateSetupIntentEndpoint
{
    public static IEndpointRouteBuilder AddStripeCreateSetupIntentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/stripe/create-setup-intent", HandleAsync)
            .AllowAnonymous()
            .WithName(SubscriptionsConstants.RouteName.CreateSetupIntentEndpoint)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSetupIntentPayment model,
        IOptions<StripeOptions> stripeOptions,
        ISubscriptionSessionStore subscriptionSessionStore,
        IStripeSetupIntentService stripeSetupIntentService)
    {
        if (string.IsNullOrEmpty(stripeOptions.Value.ApiKey))
        {
            return TypedResults.Problem("Stripe is not configured.", instance: null, statusCode: 500);
        }

        if (string.IsNullOrWhiteSpace(model.PaymentMethodId) || string.IsNullOrWhiteSpace(model.SessionId))
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
            processInitialPayment = invoice.DueNow > 0.5
        });
    }
}
