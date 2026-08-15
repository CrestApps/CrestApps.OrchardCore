using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Indexes;
using CrestApps.OrchardCore.Stripe.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using Stripe;
using YesSql;

namespace CrestApps.OrchardCore.Stripe.Endpoints;

public static class CreateWebhookEndpoint
{
    public static readonly string[] SupportedEvents =
    [
        EventTypes.InvoicePaymentSucceeded,
        EventTypes.CustomerSubscriptionCreated,
        EventTypes.PaymentIntentSucceeded,
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
        IOptions<StripeOptions> stripeOptions,
        YesSql.ISession session,
        IDistributedLock distributedLock,
        IClock clock)
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

        Event stripeEvent;

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
            logger.LogError(ex, "Unable to verify or deserialize the Stripe webhook payload.");

            return TypedResults.BadRequest();
        }

        if (stripeEvent == null || string.IsNullOrEmpty(stripeEvent.Id))
        {
            return TypedResults.BadRequest();
        }

        // Stripe delivers events at-least-once and re-delivers on any non-2xx response. Guard the
        // side-effecting handlers with a distributed lock keyed by the event id so two concurrent
        // deliveries (possibly on different instances) cannot process the same event at once, and
        // persist a processed-event marker so a later duplicate delivery is ignored. Together this
        // gives exactly-once processing semantics across a multi-instance deployment.
        var (locker, locked) = await distributedLock.TryAcquireLockAsync(
            $"STRIPE_WEBHOOK_{stripeEvent.Id}",
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(5));

        if (!locked)
        {
            // Another delivery of this same event is currently being processed. Ask Stripe to retry
            // rather than risk a concurrent double-process.
            return TypedResults.StatusCode(StatusCodes.Status409Conflict);
        }

        await using (locker)
        {
            var alreadyProcessed = await session
                .Query<ProcessedStripeWebhookEvent, ProcessedStripeWebhookEventIndex>(x => x.EventId == stripeEvent.Id)
                .FirstOrDefaultAsync();

            if (alreadyProcessed != null)
            {
                // Duplicate delivery of an event we have already processed successfully. Acknowledge
                // so Stripe stops retrying, but do not run the handlers again.
                return TypedResults.Ok();
            }

            try
            {
                await DispatchAsync(stripeEvent, paymentEvents);

                await session.SaveAsync(new ProcessedStripeWebhookEvent
                {
                    EventId = stripeEvent.Id,
                    EventType = stripeEvent.Type,
                    ProcessedUtc = clock.UtcNow,
                });

                // Commit the handler writes AND the processed-event marker together while the lock is
                // still held. If the commit happened only at the end of the request scope (after the
                // lock is released), a concurrent delivery on another instance could acquire the lock,
                // still see no marker, and process the same event a second time.
                await session.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to process Stripe webhook event '{EventId}' of type '{EventType}'. Discarding changes and returning 500 so Stripe retries.",
                    stripeEvent.Id, stripeEvent.Type);

                // Discard any partial writes the handlers may have made so the retry starts clean.
                await session.CancelAsync();

                return TypedResults.Problem("Failed to process the Stripe webhook event.", instance: null, statusCode: 500);
            }

            return TypedResults.Ok();
        }
    }

    // Dispatches the event to every payment handler directly (rather than the swallow-and-log
    // InvokeAsync helper) so that a handler failure surfaces to the caller and triggers a Stripe retry.
    internal static async Task DispatchAsync(Event stripeEvent, IEnumerable<IPaymentEvent> paymentEvents)
    {
        switch (stripeEvent.Type)
        {
            case EventTypes.InvoicePaymentSucceeded:
                if (stripeEvent.Data.Object is not Invoice invoice)
                {
                    break;
                }

                var successContext = new PaymentSucceededContext()
                {
                    AmountPaid = StripeCurrency.FromMinorUnitsToDouble(invoice.AmountPaid, invoice.Currency),
                    Currency = invoice.Currency,
                    TransactionId = invoice.Id,
                    GatewayMode = invoice.Livemode ? GatewayMode.Live : GatewayMode.Testing,
                    GatewayId = StripeConstants.ProcessorKey,
                };

                successContext.Data["billing_reason"] = invoice.BillingReason;

                foreach (var data in invoice.Metadata ?? [])
                {
                    successContext.Data[data.Key] = data.Value;
                }

                // Stripe.net moved subscription details for an invoice under Invoice.Parent in newer API versions.
                var subscriptionDetails = invoice.Parent?.SubscriptionDetails;

                successContext.Subscription = new SubscriptionPaymentInfo()
                {
                    SubscriptionId = subscriptionDetails?.SubscriptionId ?? subscriptionDetails?.Subscription?.Id,
                };

                if (subscriptionDetails != null)
                {
                    foreach (var data in subscriptionDetails.Metadata ?? [])
                    {
                        successContext.Subscription.Data[data.Key] = data.Value;
                    }
                }

                successContext.Reason = invoice.BillingReason switch
                {
                    "subscription_create" => PaymentReason.SubscriptionCreate,
                    "subscription_cycle" => PaymentReason.SubscriptionCycle,
                    "subscription_update" => PaymentReason.SubscriptionUpdate,
                    "manual" => PaymentReason.Manual,
                    _ => PaymentReason.Other,
                };

                foreach (var handler in paymentEvents)
                {
                    await handler.PaymentSucceededAsync(successContext);
                }

                break;

            case EventTypes.CustomerSubscriptionCreated:
                if (stripeEvent.Data.Object is not Subscription subscription)
                {
                    break;
                }

                var createdContext = new CustomerSubscriptionCreatedContext();

                foreach (var data in subscription.Metadata)
                {
                    createdContext.Data.Add(data.Key, data.Value);
                }

                if (subscription.Items != null && subscription.Items.Any())
                {
                    createdContext.SubscriptionId = subscription.Id;
                    createdContext.GatewayMode = subscription.Livemode ? GatewayMode.Live : GatewayMode.Testing;
                    createdContext.GatewayId = StripeConstants.ProcessorKey;
                    createdContext.PlanId = subscription.Items.Data[0].Plan.Id;
                    var plan = subscription.Items.Data[0].Plan;
                    if (plan.Amount.HasValue)
                    {
                        createdContext.PlanAmount = StripeCurrency.FromMinorUnitsToDouble(plan.Amount.Value, plan.Currency);
                    }
                    createdContext.PlanCurrency = subscription.Items.Data[0].Plan.Currency;
                    createdContext.PlanInterval = subscription.Items.Data[0].Plan.Interval;
                }

                foreach (var handler in paymentEvents)
                {
                    await handler.CustomerSubscriptionCreatedAsync(createdContext);
                }

                break;

            case EventTypes.PaymentIntentSucceeded:
                if (stripeEvent.Data.Object is not PaymentIntent paymentIntent)
                {
                    break;
                }

                var succeededContext = new PaymentIntentSucceededContext()
                {
                    TransactionId = paymentIntent.Id,
                    GatewayMode = paymentIntent.Livemode ? GatewayMode.Live : GatewayMode.Testing,
                    GatewayId = StripeConstants.ProcessorKey,
                    Currency = paymentIntent.Currency,
                    Amount = StripeCurrency.FromMinorUnitsToDouble(paymentIntent.Amount, paymentIntent.Currency),
                };

                foreach (var data in paymentIntent.Metadata)
                {
                    succeededContext.Data.Add(data.Key, data.Value);
                }

                foreach (var handler in paymentEvents)
                {
                    await handler.PaymentIntentSucceededAsync(succeededContext);
                }

                break;

            default:
                break;
        }
    }
}
