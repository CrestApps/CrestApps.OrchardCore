using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Endpoints;
using Stripe;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class CreateWebhookEndpointDispatchTests
{
    [Fact]
    public async Task DispatchAsync_PaymentIntentSucceeded_ConvertsMinorUnitsAndInvokesHandlers()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_1",
            Type = EventTypes.PaymentIntentSucceeded,
            Data = new EventData
            {
                Object = new PaymentIntent
                {
                    Id = "pi_1",
                    Amount = 2000,
                    Currency = "usd",
                    Livemode = false,
                    Metadata = new Dictionary<string, string> { ["order"] = "42" },
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        Assert.NotNull(handler.PaymentIntentContext);
        Assert.Equal("pi_1", handler.PaymentIntentContext.TransactionId);
        Assert.Equal(20.00m, handler.PaymentIntentContext.Amount);
        Assert.Equal("usd", handler.PaymentIntentContext.Currency);
        Assert.Equal(GatewayMode.Testing, handler.PaymentIntentContext.GatewayMode);
        Assert.Equal("42", handler.PaymentIntentContext.Data["order"]);
    }

    [Fact]
    public async Task DispatchAsync_ZeroDecimalCurrency_DoesNotDivide()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_jpy",
            Type = EventTypes.PaymentIntentSucceeded,
            Data = new EventData
            {
                Object = new PaymentIntent
                {
                    Id = "pi_jpy",
                    Amount = 2000,
                    Currency = "jpy",
                    Livemode = true,
                    Metadata = new Dictionary<string, string>(),
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        // JPY is a zero-decimal currency; 2000 minor units == 2000 JPY, not 20.
        Assert.Equal(2000, handler.PaymentIntentContext.Amount);
        Assert.Equal(GatewayMode.Live, handler.PaymentIntentContext.GatewayMode);
    }

    [Fact]
    public async Task DispatchAsync_InvoicePaymentSucceeded_MapsRenewalBillingReason()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_2",
            Type = EventTypes.InvoicePaymentSucceeded,
            Data = new EventData
            {
                Object = new Invoice
                {
                    Id = "in_1",
                    AmountPaid = 999,
                    Currency = "usd",
                    Livemode = false,
                    BillingReason = "subscription_cycle",
                    Metadata = new Dictionary<string, string>(),
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        Assert.NotNull(handler.PaymentSucceededContext);
        Assert.Equal(PaymentReason.SubscriptionCycle, handler.PaymentSucceededContext.Reason);
        Assert.Equal(9.99m, handler.PaymentSucceededContext.AmountPaid);
        Assert.Equal("subscription_cycle", handler.PaymentSucceededContext.Data["billing_reason"]);
    }

    [Fact]
    public async Task DispatchAsync_ChargeRefunded_EmitsOneRefundPerExpandedRefund()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_refund",
            Type = EventTypes.ChargeRefunded,
            Request = new EventRequest { IdempotencyKey = "req-key" },
            Data = new EventData
            {
                Object = new Charge
                {
                    Id = "ch_1",
                    PaymentIntentId = "pi_1",
                    Currency = "usd",
                    AmountRefunded = 3000,
                    Livemode = true,
                    Refunds = new StripeList<Refund>
                    {
                        Data =
                        [
                            new Refund { Id = "re_1", Amount = 1000, Currency = "usd", PaymentIntentId = "pi_1", Status = "succeeded", Reason = "requested_by_customer" },
                            new Refund { Id = "re_2", Amount = 2000, Currency = "usd", PaymentIntentId = "pi_1", Status = "pending" },
                        ],
                    },
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        Assert.Equal(2, handler.RefundContexts.Count);

        Assert.Equal("re_1", handler.RefundContexts[0].ProviderRefundReference);
        Assert.Equal("pi_1", handler.RefundContexts[0].OriginalTransactionId);
        Assert.Equal(10.00m, handler.RefundContexts[0].RefundedAmount);
        Assert.Equal("succeeded", handler.RefundContexts[0].RefundStatus);
        Assert.Equal(GatewayMode.Live, handler.RefundContexts[0].GatewayMode);

        Assert.Equal("re_2", handler.RefundContexts[1].ProviderRefundReference);
        Assert.Equal(20.00m, handler.RefundContexts[1].RefundedAmount);
        Assert.Equal("pending", handler.RefundContexts[1].RefundStatus);

        // The charge event's request idempotency key belongs to the single API call that triggered the
        // event, not to every historical refund on the charge, so it is never stamped onto an expanded
        // refund. Each expanded refund correlates by its own provider reference and metadata instead.
        Assert.Null(handler.RefundContexts[0].IdempotencyKey);
        Assert.Null(handler.RefundContexts[1].IdempotencyKey);
    }

    [Fact]
    public async Task DispatchAsync_ChargeRefunded_WithoutExpandedRefunds_EmitsAggregate()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_refund_agg",
            Type = EventTypes.ChargeRefunded,
            Request = new EventRequest { IdempotencyKey = "req-key-agg" },
            Data = new EventData
            {
                Object = new Charge
                {
                    Id = "ch_2",
                    PaymentIntentId = "pi_2",
                    Currency = "usd",
                    AmountRefunded = 1500,
                    Livemode = false,
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        var refund = Assert.Single(handler.RefundContexts);
        Assert.Equal("pi_2", refund.OriginalTransactionId);
        Assert.Equal(15.00m, refund.RefundedAmount);
        Assert.Null(refund.ProviderRefundReference);

        // The charge event's request idempotency key belongs to a single API call, not the charge's whole
        // refund history, so it is never stamped onto the identity-less aggregate. The aggregate is instead
        // correlated by a deterministic provider/mode/transaction/currency key.
        Assert.Null(refund.IdempotencyKey);
    }

    [Fact]
    public async Task DispatchAsync_PaymentIntentPaymentFailed_MapsFailureCodeAndReason()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_failed",
            Type = EventTypes.PaymentIntentPaymentFailed,
            Data = new EventData
            {
                Object = new PaymentIntent
                {
                    Id = "pi_fail",
                    Amount = 5000,
                    Currency = "usd",
                    Livemode = true,
                    Metadata = new Dictionary<string, string>(),
                    LastPaymentError = new StripeError { Code = "card_declined", Message = "Your card was declined." },
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        Assert.NotNull(handler.FailedContext);
        Assert.Equal("pi_fail", handler.FailedContext.TransactionId);
        Assert.Equal(50.00m, handler.FailedContext.Amount);
        Assert.Equal("card_declined", handler.FailedContext.FailureCode);
        Assert.Equal("Your card was declined.", handler.FailedContext.FailureReason);
    }

    [Fact]
    public async Task DispatchAsync_PaymentIntentCanceled_MapsCancellationReason()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_canceled",
            Type = EventTypes.PaymentIntentCanceled,
            Data = new EventData
            {
                Object = new PaymentIntent
                {
                    Id = "pi_cancel",
                    Currency = "usd",
                    Livemode = false,
                    Metadata = new Dictionary<string, string>(),
                    CancellationReason = "abandoned",
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        Assert.NotNull(handler.CanceledContext);
        Assert.Equal("pi_cancel", handler.CanceledContext.TransactionId);
        Assert.Equal("abandoned", handler.CanceledContext.Reason);
    }

    [Fact]
    public async Task DispatchAsync_ChargeDisputeCreated_MapsDisputeDetails()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_dispute",
            Type = EventTypes.ChargeDisputeCreated,
            Data = new EventData
            {
                Object = new Dispute
                {
                    Id = "dp_1",
                    PaymentIntentId = "pi_3",
                    Amount = 4000,
                    Currency = "usd",
                    Livemode = true,
                    Reason = "fraudulent",
                    Status = "needs_response",
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        Assert.NotNull(handler.DisputeContext);
        Assert.Equal("pi_3", handler.DisputeContext.OriginalTransactionId);
        Assert.Equal("dp_1", handler.DisputeContext.DisputeReference);
        Assert.Equal(40.00m, handler.DisputeContext.Amount);
        Assert.Equal("fraudulent", handler.DisputeContext.Reason);
        Assert.Equal("needs_response", handler.DisputeContext.Status);
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrows_PropagatesSoStripeRetries()
    {
        var throwing = new ThrowingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_3",
            Type = EventTypes.PaymentIntentSucceeded,
            Data = new EventData
            {
                Object = new PaymentIntent
                {
                    Id = "pi_2",
                    Amount = 100,
                    Currency = "usd",
                    Metadata = new Dictionary<string, string>(),
                },
            },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateWebhookEndpoint.DispatchAsync(stripeEvent, [throwing]));
    }

    [Fact]
    public async Task DispatchAsync_UnhandledEventType_DoesNothing()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_4",
            Type = "customer.updated",
            Data = new EventData
            {
                Object = new PaymentIntent { Id = "pi_x", Currency = "usd", Metadata = new Dictionary<string, string>() },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        Assert.Null(handler.PaymentIntentContext);
        Assert.Null(handler.PaymentSucceededContext);
        Assert.Empty(handler.RefundContexts);
    }

    [Fact]
    public async Task DispatchAsync_RefundUpdated_MapsSingleRefundWithMetadata()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_refund_updated",
            Type = EventTypes.RefundUpdated,
            Livemode = true,
            Data = new EventData
            {
                Object = new Refund
                {
                    Id = "re_async",
                    PaymentIntentId = "pi_async",
                    Amount = 2500,
                    Currency = "usd",
                    Status = "succeeded",
                    Reason = "requested_by_customer",
                    Metadata = new Dictionary<string, string> { ["checkout_refund_idempotency_key"] = "idem-async" },
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        var refund = Assert.Single(handler.RefundContexts);
        Assert.Equal("re_async", refund.ProviderRefundReference);
        Assert.Equal("pi_async", refund.OriginalTransactionId);
        Assert.Equal(25.00m, refund.RefundedAmount);
        Assert.Equal("succeeded", refund.RefundStatus);
        Assert.Equal(GatewayMode.Live, refund.GatewayMode);
        Assert.Equal("idem-async", refund.Data["checkout_refund_idempotency_key"]);
    }

    [Fact]
    public async Task DispatchAsync_RefundFailed_MapsFailedStatusAndFallsBackToChargeId()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_refund_failed",
            Type = EventTypes.RefundFailed,
            Livemode = false,
            Data = new EventData
            {
                Object = new Refund
                {
                    Id = "re_failed",
                    ChargeId = "ch_only",
                    Amount = 500,
                    Currency = "usd",
                    Status = "failed",
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        var refund = Assert.Single(handler.RefundContexts);
        Assert.Equal("re_failed", refund.ProviderRefundReference);
        Assert.Equal("ch_only", refund.OriginalTransactionId);
        Assert.Equal("failed", refund.RefundStatus);
        Assert.Equal(GatewayMode.Testing, refund.GatewayMode);
    }

    [Fact]
    public async Task DispatchAsync_RefundCreated_MapsSingleRefund()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_refund_created",
            Type = EventTypes.RefundCreated,
            Livemode = true,
            Data = new EventData
            {
                Object = new Refund
                {
                    Id = "re_created",
                    PaymentIntentId = "pi_created",
                    Amount = 1500,
                    Currency = "usd",
                    Status = "pending",
                    Metadata = new Dictionary<string, string> { ["checkout_refund_idempotency_key"] = "idem-created" },
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        var refund = Assert.Single(handler.RefundContexts);
        Assert.Equal("re_created", refund.ProviderRefundReference);
        Assert.Equal("pi_created", refund.OriginalTransactionId);
        Assert.Equal(15.00m, refund.RefundedAmount);
        Assert.Equal("pending", refund.RefundStatus);
        Assert.Equal(GatewayMode.Live, refund.GatewayMode);
        Assert.Equal("idem-created", refund.Data["checkout_refund_idempotency_key"]);
    }

    [Fact]
    public async Task DispatchAsync_ChargeRefunded_FallsBackToChargeId_WhenNoPaymentIntent()
    {
        var handler = new RecordingPaymentEvent();

        var stripeEvent = new Event
        {
            Id = "evt_refund_charge_only",
            Type = EventTypes.ChargeRefunded,
            Data = new EventData
            {
                Object = new Charge
                {
                    Id = "ch_legacy",
                    Currency = "usd",
                    AmountRefunded = 700,
                    Livemode = false,
                    Metadata = new Dictionary<string, string> { ["checkout_refund_idempotency_key"] = "idem-charge" },
                },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        var refund = Assert.Single(handler.RefundContexts);
        Assert.Equal("ch_legacy", refund.OriginalTransactionId);
        Assert.Equal(7.00m, refund.RefundedAmount);
        Assert.Null(refund.ProviderRefundReference);
        Assert.Equal("idem-charge", refund.Data["checkout_refund_idempotency_key"]);
    }

    private sealed class RecordingPaymentEvent : PaymentEventBase
    {
        public PaymentSucceededContext PaymentSucceededContext { get; private set; }

        public PaymentIntentSucceededContext PaymentIntentContext { get; private set; }

        public PaymentFailedContext FailedContext { get; private set; }

        public PaymentCanceledContext CanceledContext { get; private set; }

        public PaymentDisputeCreatedContext DisputeContext { get; private set; }

        public List<PaymentRefundedContext> RefundContexts { get; } = [];

        public override Task PaymentSucceededAsync(PaymentSucceededContext context)
        {
            PaymentSucceededContext = context;

            return Task.CompletedTask;
        }

        public override Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context)
        {
            PaymentIntentContext = context;

            return Task.CompletedTask;
        }

        public override Task PaymentFailedAsync(PaymentFailedContext context)
        {
            FailedContext = context;

            return Task.CompletedTask;
        }

        public override Task PaymentCanceledAsync(PaymentCanceledContext context)
        {
            CanceledContext = context;

            return Task.CompletedTask;
        }

        public override Task PaymentRefundedAsync(PaymentRefundedContext context)
        {
            RefundContexts.Add(context);

            return Task.CompletedTask;
        }

        public override Task PaymentDisputeCreatedAsync(PaymentDisputeCreatedContext context)
        {
            DisputeContext = context;

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPaymentEvent : PaymentEventBase
    {
        public override Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context)
            => throw new InvalidOperationException("boom");
    }
}
