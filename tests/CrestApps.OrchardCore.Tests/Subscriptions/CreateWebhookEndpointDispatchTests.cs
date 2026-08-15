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
        Assert.Equal(20.00, handler.PaymentIntentContext.Amount);
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
        Assert.Equal(9.99, handler.PaymentSucceededContext.AmountPaid);
        Assert.Equal("subscription_cycle", handler.PaymentSucceededContext.Data["billing_reason"]);
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
            Type = "charge.refunded",
            Data = new EventData
            {
                Object = new PaymentIntent { Id = "pi_x", Currency = "usd", Metadata = new Dictionary<string, string>() },
            },
        };

        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler]);

        Assert.Null(handler.PaymentIntentContext);
        Assert.Null(handler.PaymentSucceededContext);
    }

    private sealed class RecordingPaymentEvent : IPaymentEvent
    {
        public PaymentSucceededContext PaymentSucceededContext { get; private set; }

        public CustomerSubscriptionCreatedContext CustomerSubscriptionCreatedContext { get; private set; }

        public PaymentIntentSucceededContext PaymentIntentContext { get; private set; }

        public Task PaymentSucceededAsync(PaymentSucceededContext context)
        {
            PaymentSucceededContext = context;
            return Task.CompletedTask;
        }

        public Task CustomerSubscriptionCreatedAsync(CustomerSubscriptionCreatedContext context)
        {
            CustomerSubscriptionCreatedContext = context;
            return Task.CompletedTask;
        }

        public Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context)
        {
            PaymentIntentContext = context;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPaymentEvent : IPaymentEvent
    {
        public Task PaymentSucceededAsync(PaymentSucceededContext context)
            => throw new InvalidOperationException("boom");

        public Task CustomerSubscriptionCreatedAsync(CustomerSubscriptionCreatedContext context)
            => throw new InvalidOperationException("boom");

        public Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context)
            => throw new InvalidOperationException("boom");
    }
}
