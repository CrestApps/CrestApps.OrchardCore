using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Endpoints;

public static class CreateWebhookEndpoint
{
    public static readonly string[] SupportedEvents =
    [
        Events.InvoicePaymentSucceeded,
        Events.CustomerSubscriptionCreated,
        Events.PaymentIntentSucceeded,
    ];

    public static IEndpointRouteBuilder AddWebhookEndpoint<T>(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("stripe/webhook", HandleAsync<T>)
            .AllowAnonymous()
            .WithName(StripeConstants.RouteName.CreateWebhookEndpoint)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync<T>(
        IHttpContextAccessor httpContextAccessor,
        ILogger<T> logger,
        IEnumerable<IPaymentEvent> paymentEvents,
        IOptions<StripeOptions> stripeOptions)
    {
        var request = httpContextAccessor.HttpContext.Request;
        var json = await new StreamReader(request.Body).ReadToEndAsync();

        if (!request.Headers.TryGetValue("Stripe-Signature", out var signature) ||
            string.IsNullOrEmpty(signature))
        {
            return TypedResults.BadRequest();
        }

        if (string.IsNullOrEmpty(stripeOptions.Value.WebhookSecret))
        {
            return TypedResults.Problem("Stripe is not configured.", instance: null, statusCode: 500);
        }

        try
        {
            Event stripeEvent = null;

            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                json: json,
                stripeSignatureHeader: signature,
                stripeOptions.Value.WebhookSecret,
                throwOnApiVersionMismatch: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to deserialize Stripe hook response.");

                return TypedResults.BadRequest();
            }

            if (stripeEvent == null)
            {
                return TypedResults.BadRequest();
            }

            switch (stripeEvent.Type)
            {
                case Events.InvoicePaymentSucceeded:
                    var invoice = stripeEvent.Data.Object as Invoice;

                    var successContext = new PaymentSucceededContext()
                    {
                        AmountPaid = invoice.AmountPaid / 100.0,
                        Currency = invoice.Currency,
                    };

                    foreach (var data in invoice.Metadata)
                    {
                        successContext.Data.Add(data.Key, data.Value);
                    }
                    await paymentEvents.InvokeAsync((handler, context) => handler.PaymentSucceededAsync(context), successContext, logger);
                    break;

                case Events.CustomerSubscriptionCreated:
                    var subscription = stripeEvent.Data.Object as Subscription;
                    var createdContext = new CustomerSubscriptionCreatedContext();

                    foreach (var data in subscription.Metadata)
                    {
                        createdContext.Data.Add(data.Key, data.Value);
                    }

                    if (subscription.Items != null && subscription.Items.Any())
                    {
                        createdContext.PlanId = subscription.Items.Data[0].Plan.Id;
                        createdContext.PlanAmount = subscription.Items.Data[0].Plan.Amount / 100.0; // Amount in dollars
                        createdContext.PlanCurrency = subscription.Items.Data[0].Plan.Currency;
                        createdContext.PlanInterval = subscription.Items.Data[0].Plan.Interval;
                    }

                    await paymentEvents.InvokeAsync((handler, context) => handler.CustomerSubscriptionCreatedAsync(context), createdContext, logger);
                    break;

                case Events.PaymentIntentSucceeded:
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    var succeededContext = new PaymentIntentSucceededContext();

                    foreach (var data in paymentIntent.Metadata)
                    {
                        succeededContext.Data.Add(data.Key, data.Value);
                    }
                    await paymentEvents.InvokeAsync((handler, context) => handler.PaymentIntentSucceededAsync(context), succeededContext, logger);
                    break;

                default:
                    break;
            }

            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invalid Stripe Webhook call.");
        }

        return TypedResults.BadRequest();
    }
}
