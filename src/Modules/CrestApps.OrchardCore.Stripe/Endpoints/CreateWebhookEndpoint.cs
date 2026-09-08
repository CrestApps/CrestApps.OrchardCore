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

/// <summary>
/// Registers and handles the Stripe webhook endpoint used to dispatch supported payment events.
/// </summary>
public static class CreateWebhookEndpoint
{
    /// <summary>
    /// The Stripe event types accepted by the webhook dispatcher.
    /// </summary>
    public static readonly string[] SupportedEvents =
    [
        EventTypes.InvoicePaymentSucceeded,
        EventTypes.InvoicePaymentFailed,
        EventTypes.CustomerSubscriptionCreated,
        EventTypes.CustomerSubscriptionUpdated,
        EventTypes.CustomerSubscriptionDeleted,
        EventTypes.PaymentIntentSucceeded,
        EventTypes.PaymentIntentPaymentFailed,
        EventTypes.PaymentIntentCanceled,
        EventTypes.ChargeRefunded,
        EventTypes.ChargeRefundUpdated,
        EventTypes.RefundCreated,
        EventTypes.RefundUpdated,
        EventTypes.RefundFailed,
        EventTypes.ChargeDisputeCreated,
    ];

    /// <summary>
    /// Adds the Stripe webhook endpoint to the endpoint route builder.
    /// </summary>
    /// <typeparam name="T">The category type used for webhook logging.</typeparam>
    /// <param name="builder">The endpoint route builder to update.</param>
    /// <returns>The endpoint route builder after the Stripe webhook route is registered.</returns>
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
        IClock clock,
        CancellationToken cancellationToken)
    {
        var request = httpContextAccessor.HttpContext.Request;
        var json = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);

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
                .FirstOrDefaultAsync(cancellationToken);

            if (alreadyProcessed != null)
            {
                // Duplicate delivery of an event we have already processed successfully. Acknowledge
                // so Stripe stops retrying, but do not run the handlers again.
                return TypedResults.Ok();
            }

            try
            {
                await DispatchAsync(stripeEvent, paymentEvents, cancellationToken);

                await session.SaveAsync(new ProcessedStripeWebhookEvent
                {
                    EventId = stripeEvent.Id,
                    EventType = stripeEvent.Type,
                    ProcessedUtc = clock.UtcNow,
                }, cancellationToken: CancellationToken.None);

                // Commit the handler writes AND the processed-event marker together while the lock is
                // still held. If the commit happened only at the end of the request scope (after the
                // lock is released), a concurrent delivery on another instance could acquire the lock,
                // still see no marker, and process the same event a second time.
                //
                // The commit deliberately ignores the request token. The handlers have already run by this
                // point, so abandoning the write because the gateway hung up would discard the record that
                // they ran and let the retried delivery execute them a second time.
                await session.SaveChangesAsync(CancellationToken.None);
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
    internal static async Task DispatchAsync(Event stripeEvent, IEnumerable<IPaymentEvent> paymentEvents, CancellationToken cancellationToken = default)
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
                    AmountPaid = StripeCurrency.FromMinorUnits(invoice.AmountPaid, invoice.Currency),
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

                // The period comes from the subscription line when there is one: on a cycle invoice that is
                // the period being billed, whereas the invoice's own period can straddle a proration.
                var linePeriod = invoice.Lines?.Data?.FirstOrDefault(line => line.Period is not null)?.Period;

                successContext.Subscription = new SubscriptionPaymentInfo()
                {
                    SubscriptionId = subscriptionDetails?.SubscriptionId ?? subscriptionDetails?.Subscription?.Id,
                    PeriodStartUtc = linePeriod?.Start ?? invoice.PeriodStart,
                    PeriodEndUtc = linePeriod?.End ?? invoice.PeriodEnd,
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
                    await handler.PaymentSucceededAsync(successContext, cancellationToken);
                }

                break;

            case EventTypes.InvoicePaymentFailed:
                if (stripeEvent.Data.Object is not Invoice failedInvoice)
                {
                    break;
                }

                // Only a renewal of an existing subscription is reported here. A failed first payment is not a
                // subscription at risk: the checkout simply never completes, and the existing payment-failure
                // path already covers it.
                var failedSubscriptionDetails = failedInvoice.Parent?.SubscriptionDetails;
                var failedSubscriptionId = failedSubscriptionDetails?.SubscriptionId ?? failedSubscriptionDetails?.Subscription?.Id;

                if (string.IsNullOrEmpty(failedSubscriptionId))
                {
                    break;
                }

                var subscriptionFailedContext = new SubscriptionPaymentFailedContext()
                {
                    SubscriptionId = failedSubscriptionId,
                    TransactionId = failedInvoice.Id,
                    Amount = StripeCurrency.FromMinorUnits(failedInvoice.AmountDue, failedInvoice.Currency),
                    Currency = failedInvoice.Currency,
                    GatewayMode = failedInvoice.Livemode ? GatewayMode.Live : GatewayMode.Testing,
                    GatewayId = StripeConstants.ProcessorKey,
                    AttemptCount = (int?)failedInvoice.AttemptCount,
                    NextAttemptUtc = failedInvoice.NextPaymentAttempt?.ToUniversalTime(),
                };

                foreach (var data in failedInvoice.Metadata ?? [])
                {
                    subscriptionFailedContext.Data[data.Key] = data.Value;
                }

                foreach (var data in failedSubscriptionDetails?.Metadata ?? [])
                {
                    subscriptionFailedContext.Data[data.Key] = data.Value;
                }

                foreach (var handler in paymentEvents)
                {
                    await handler.SubscriptionPaymentFailedAsync(subscriptionFailedContext, cancellationToken);
                }

                break;

            case EventTypes.CustomerSubscriptionUpdated:
            case EventTypes.CustomerSubscriptionDeleted:
                if (stripeEvent.Data.Object is not Subscription changedSubscription)
                {
                    break;
                }

                // A deletion event always means the agreement is gone, whatever status the object still
                // carries; an update event reports whatever state Stripe moved it to.
                var status = stripeEvent.Type == EventTypes.CustomerSubscriptionDeleted
                    ? RemoteSubscriptionStatus.Canceled
                    : MapSubscriptionStatus(changedSubscription.Status);

                var statusContext = new SubscriptionStatusChangedContext()
                {
                    SubscriptionId = changedSubscription.Id,
                    Status = status,
                    CurrentPeriodEndUtc = GetCurrentPeriodEnd(changedSubscription)?.ToUniversalTime(),
                    CanceledUtc = changedSubscription.CanceledAt?.ToUniversalTime(),
                    CancelAtPeriodEnd = changedSubscription.CancelAtPeriodEnd,
                    GatewayMode = changedSubscription.Livemode ? GatewayMode.Live : GatewayMode.Testing,
                    GatewayId = StripeConstants.ProcessorKey,
                };

                foreach (var data in changedSubscription.Metadata ?? [])
                {
                    statusContext.Data[data.Key] = data.Value;
                }

                foreach (var handler in paymentEvents)
                {
                    await handler.SubscriptionStatusChangedAsync(statusContext, cancellationToken);
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
                        createdContext.PlanAmount = StripeCurrency.FromMinorUnits(plan.Amount.Value, plan.Currency);
                    }
                    createdContext.PlanCurrency = subscription.Items.Data[0].Plan.Currency;
                    createdContext.PlanInterval = subscription.Items.Data[0].Plan.Interval;
                }

                foreach (var handler in paymentEvents)
                {
                    await handler.CustomerSubscriptionCreatedAsync(createdContext, cancellationToken);
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
                    Amount = StripeCurrency.FromMinorUnits(paymentIntent.Amount, paymentIntent.Currency),
                };

                foreach (var data in paymentIntent.Metadata)
                {
                    succeededContext.Data.Add(data.Key, data.Value);
                }

                foreach (var handler in paymentEvents)
                {
                    await handler.PaymentIntentSucceededAsync(succeededContext, cancellationToken);
                }

                break;

            case EventTypes.PaymentIntentPaymentFailed:
                if (stripeEvent.Data.Object is not PaymentIntent failedIntent)
                {
                    break;
                }

                var failedContext = new PaymentFailedContext()
                {
                    TransactionId = failedIntent.Id,
                    GatewayMode = failedIntent.Livemode ? GatewayMode.Live : GatewayMode.Testing,
                    GatewayId = StripeConstants.ProcessorKey,
                    Currency = failedIntent.Currency,
                    Amount = StripeCurrency.FromMinorUnits(failedIntent.Amount, failedIntent.Currency),
                    FailureCode = failedIntent.LastPaymentError?.Code,
                    FailureReason = failedIntent.LastPaymentError?.Message,
                };

                foreach (var data in failedIntent.Metadata ?? [])
                {
                    failedContext.Data[data.Key] = data.Value;
                }

                foreach (var handler in paymentEvents)
                {
                    await handler.PaymentFailedAsync(failedContext, cancellationToken);
                }

                break;

            case EventTypes.PaymentIntentCanceled:
                if (stripeEvent.Data.Object is not PaymentIntent canceledIntent)
                {
                    break;
                }

                var canceledContext = new PaymentCanceledContext()
                {
                    TransactionId = canceledIntent.Id,
                    GatewayMode = canceledIntent.Livemode ? GatewayMode.Live : GatewayMode.Testing,
                    GatewayId = StripeConstants.ProcessorKey,
                    Currency = canceledIntent.Currency,
                    Reason = canceledIntent.CancellationReason,
                };

                foreach (var data in canceledIntent.Metadata ?? [])
                {
                    canceledContext.Data[data.Key] = data.Value;
                }

                foreach (var handler in paymentEvents)
                {
                    await handler.PaymentCanceledAsync(canceledContext, cancellationToken);
                }

                break;

            case EventTypes.ChargeRefunded:
                if (stripeEvent.Data.Object is not Charge charge)
                {
                    break;
                }

                foreach (var refundContext in BuildRefundContexts(charge))
                {
                    foreach (var handler in paymentEvents)
                    {
                        await handler.PaymentRefundedAsync(refundContext, cancellationToken);
                    }
                }

                break;

            case EventTypes.ChargeRefundUpdated:
            case EventTypes.RefundCreated:
            case EventTypes.RefundUpdated:
            case EventTypes.RefundFailed:
                if (stripeEvent.Data.Object is not Refund updatedRefund)
                {
                    break;
                }

                // The event's request idempotency key is the key of the API call that produced the event.
                // It correlates a locally initiated refund only for the creation event; on an update or
                // failure event the key belongs to that later request, not to the refund's creation, so the
                // refund is correlated by its own provider reference and its own metadata instead.
                var refundIdempotencyKey = stripeEvent.Type == EventTypes.RefundCreated
                    ? stripeEvent.Request?.IdempotencyKey
                    : null;

                var updatedRefundContext = BuildRefundContext(
                    updatedRefund,
                    stripeEvent.Livemode ? GatewayMode.Live : GatewayMode.Testing,
                    refundIdempotencyKey);

                foreach (var handler in paymentEvents)
                {
                    await handler.PaymentRefundedAsync(updatedRefundContext, cancellationToken);
                }

                break;

            case EventTypes.ChargeDisputeCreated:
                if (stripeEvent.Data.Object is not Dispute dispute)
                {
                    break;
                }

                var disputeContext = new PaymentDisputeCreatedContext()
                {
                    OriginalTransactionId = dispute.PaymentIntentId ?? dispute.ChargeId,
                    DisputeReference = dispute.Id,
                    GatewayMode = dispute.Livemode ? GatewayMode.Live : GatewayMode.Testing,
                    GatewayId = StripeConstants.ProcessorKey,
                    Currency = dispute.Currency,
                    Amount = StripeCurrency.FromMinorUnits(dispute.Amount, dispute.Currency),
                    Reason = dispute.Reason,
                    Status = dispute.Status,
                };

                foreach (var handler in paymentEvents)
                {
                    await handler.PaymentDisputeCreatedAsync(disputeContext, cancellationToken);
                }

                break;

            default:
                break;
        }
    }

    // Maps Stripe's subscription status vocabulary onto the provider-neutral one. An unrecognized value maps to
    // Unknown rather than to a guess, so a consumer leaves its local state untouched instead of acting on a
    // status this adapter does not understand.
    private static RemoteSubscriptionStatus MapSubscriptionStatus(string status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "trialing" => RemoteSubscriptionStatus.Trialing,
            "active" => RemoteSubscriptionStatus.Active,
            "past_due" => RemoteSubscriptionStatus.PastDue,
            "unpaid" => RemoteSubscriptionStatus.Unpaid,
            "canceled" or "cancelled" => RemoteSubscriptionStatus.Canceled,
            "incomplete" => RemoteSubscriptionStatus.Incomplete,
            "incomplete_expired" => RemoteSubscriptionStatus.IncompleteExpired,
            "paused" => RemoteSubscriptionStatus.Paused,
            _ => RemoteSubscriptionStatus.Unknown,
        };

    // Reads the end of the current paid period. Newer Stripe API versions moved the period boundaries from the
    // subscription onto its items, so the subscription-level value is preferred and the items are used as the
    // fallback. The latest end across items is used, because that is the date every item is paid through.
    private static DateTime? GetCurrentPeriodEnd(Subscription subscription)
    {
        var items = subscription.Items?.Data;

        if (items is null || items.Count == 0)
        {
            return null;
        }

        DateTime? latest = null;

        foreach (var item in items)
        {
            if (item.CurrentPeriodEnd > (latest ?? DateTime.MinValue))
            {
                latest = item.CurrentPeriodEnd;
            }
        }

        return latest;
    }

    // Maps a Stripe charge.refunded event into one provider-neutral refund notification per refund on the
    // charge, so every remote refund (including one issued from the Stripe dashboard) is reconciled against
    // the durable refund ledger. When the charge carries no expanded refund detail, a single aggregate
    // notification is emitted from the charge's refunded total so the refund is never silently dropped. Each
    // expanded refund is correlated by its own provider reference and metadata, and the identity-less
    // aggregate is correlated by a deterministic provider/mode/transaction/currency key, so the charge
    // event's request idempotency key (which belongs to a single API call, not to the charge's whole refund
    // history) is never forwarded here.
    private static IEnumerable<PaymentRefundedContext> BuildRefundContexts(Charge charge)
    {
        var refunds = charge.Refunds?.Data;

        if (refunds is not null && refunds.Count > 0)
        {
            foreach (var refund in refunds)
            {
                var context = new PaymentRefundedContext()
                {
                    OriginalTransactionId = refund.PaymentIntentId ?? charge.PaymentIntentId ?? charge.Id,
                    ProviderRefundReference = refund.Id,
                    GatewayMode = charge.Livemode ? GatewayMode.Live : GatewayMode.Testing,
                    GatewayId = StripeConstants.ProcessorKey,
                    Currency = refund.Currency ?? charge.Currency,
                    RefundedAmount = StripeCurrency.FromMinorUnits(refund.Amount, refund.Currency ?? charge.Currency),
                    RefundStatus = refund.Status,
                    Reason = refund.Reason,
                };

                CopyMetadata(context, refund.Metadata);

                yield return context;
            }

            yield break;
        }

        var aggregate = new PaymentRefundedContext()
        {
            OriginalTransactionId = charge.PaymentIntentId ?? charge.Id,
            GatewayMode = charge.Livemode ? GatewayMode.Live : GatewayMode.Testing,
            GatewayId = StripeConstants.ProcessorKey,
            Currency = charge.Currency,
            RefundedAmount = StripeCurrency.FromMinorUnits(charge.AmountRefunded, charge.Currency),
        };

        CopyMetadata(aggregate, charge.Metadata);

        yield return aggregate;
    }

    // Maps a single Stripe refund object (delivered by refund.created, refund.updated, refund.failed, or
    // charge.refund.updated) into a provider-neutral refund notification, so an asynchronous refund that
    // settles or fails after the initial charge.refunded event is reconciled to the durable ledger. The
    // refund object does not carry its own live/test flag, so the owning event's mode is passed in, along
    // with the event's idempotency key when the refund was created by this application.
    private static PaymentRefundedContext BuildRefundContext(Refund refund, GatewayMode gatewayMode, string idempotencyKey)
    {
        var context = new PaymentRefundedContext()
        {
            OriginalTransactionId = refund.PaymentIntentId ?? refund.ChargeId,
            ProviderRefundReference = refund.Id,
            GatewayMode = gatewayMode,
            GatewayId = StripeConstants.ProcessorKey,
            Currency = refund.Currency,
            RefundedAmount = StripeCurrency.FromMinorUnits(refund.Amount, refund.Currency),
            RefundStatus = refund.Status,
            Reason = refund.Reason,
            IdempotencyKey = idempotencyKey,
        };

        CopyMetadata(context, refund.Metadata);

        return context;
    }

    // Forwards gateway metadata verbatim onto the provider-neutral notification, so the checkout can
    // correlate a remote refund by its own metadata keys without the provider adapter interpreting them.
    private static void CopyMetadata(PaymentRefundedContext context, IDictionary<string, string> metadata)
    {
        if (metadata is null)
        {
            return;
        }

        foreach (var pair in metadata)
        {
            context.Data[pair.Key] = pair.Value;
        }
    }
}
