using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Exceptions;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class PaymentSubscriptionHandlerTests
{
    private const string Currency = "USD";

    [Fact]
    public async Task ActivatedAsync_BuildsInvoiceFromBillingItems()
    {
        var handler = CreateHandler(PaymentTestHelpers.CreatePaymentSession());

        var session = CreateSession(
            OneTimeStep("content", 19.99),
            SubscriptionStep("plan", 10.00, dayDelay: 0));

        var flow = new SubscriptionFlow(session, new ContentItem());

        await handler.ActivatedAsync(new SubscriptionFlowActivatedContext(flow));

        Assert.True(session.TryGet<Invoice>(out var invoice));
        Assert.Equal(Currency, invoice.Currency);
        Assert.Equal(19.99, invoice.InitialPaymentAmount);
        Assert.Equal(10.00, invoice.FirstSubscriptionPaymentAmount);
        Assert.Equal(29.99, invoice.DueNow);
        Assert.Equal(29.99, invoice.GrandTotal);
        Assert.Equal(2, invoice.LineItems.Length);
    }

    [Fact]
    public async Task ActivatedAsync_DelayedSubscription_IsNotDueNow()
    {
        var handler = CreateHandler(PaymentTestHelpers.CreatePaymentSession());

        var session = CreateSession(
            OneTimeStep("content", 25.00),
            SubscriptionStep("plan", 10.00, dayDelay: 30));

        var flow = new SubscriptionFlow(session, new ContentItem());

        await handler.ActivatedAsync(new SubscriptionFlowActivatedContext(flow));

        Assert.True(session.TryGet<Invoice>(out var invoice));
        Assert.Equal(25.00, invoice.InitialPaymentAmount);
        // A delayed subscription is not collected on the first payment.
        Assert.Null(invoice.FirstSubscriptionPaymentAmount);
        Assert.Equal(25.00, invoice.DueNow);
    }

    [Fact]
    public async Task CompletingAsync_WhenInitialPaymentMatches_StoresPaymentAndDoesNotThrow()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();
        var handler = CreateHandler(paymentSession);

        var session = CreateSession(OneTimeStep("content", 19.99));
        session.SessionId = "session-1";

        session.Put(new Invoice
        {
            Currency = Currency,
            InitialPaymentAmount = 19.99,
            DueNow = 19.99,
            GrandTotal = 19.99,
            LineItems = [],
        });

        // The payment gateway reported an amount that is only equal to the invoice at cent precision.
        await paymentSession.SetAsync(session.SessionId, new InitialPaymentMetadata
        {
            TransactionId = "pi_1",
            Amount = 10.00 + 9.99,
            Currency = Currency,
            GatewayId = "stripe",
        });

        var flow = new SubscriptionFlow(session, new ContentItem());

        await handler.CompletingAsync(new SubscriptionFlowCompletingContext(flow));

        Assert.True(session.TryGet<PaymentsMetadata>(out var payments));
        Assert.True(payments.Payments.ContainsKey("pi_1"));
        Assert.Equal(PaymentStatus.Succeeded, payments.Payments["pi_1"].Status);
        Assert.Equal(19.99, payments.Payments["pi_1"].Amount, 2);
    }

    [Fact]
    public async Task CompletingAsync_WhenInitialPaymentAmountMismatches_ThrowsPaymentValidationException()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();
        var handler = CreateHandler(paymentSession);

        var session = CreateSession(OneTimeStep("content", 19.99));
        session.SessionId = "session-2";

        session.Put(new Invoice
        {
            Currency = Currency,
            InitialPaymentAmount = 19.99,
            DueNow = 19.99,
            GrandTotal = 19.99,
            LineItems = [],
        });

        await paymentSession.SetAsync(session.SessionId, new InitialPaymentMetadata
        {
            TransactionId = "pi_2",
            Amount = 18.00,
            Currency = Currency,
            GatewayId = "stripe",
        });

        var flow = new SubscriptionFlow(session, new ContentItem());

        await Assert.ThrowsAsync<PaymentValidationException>(
            () => handler.CompletingAsync(new SubscriptionFlowCompletingContext(flow)));
    }

    [Fact]
    public async Task CompletingAsync_WhenSubscriptionPaymentsMatch_StoresAllPayments()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();
        var handler = CreateHandler(paymentSession);

        var session = CreateSession(SubscriptionStep("plan", 30.00, dayDelay: 0));
        session.SessionId = "session-3";

        session.Put(new Invoice
        {
            Currency = Currency,
            FirstSubscriptionPaymentAmount = 30.00,
            DueNow = 30.00,
            GrandTotal = 30.00,
            LineItems = [],
        });

        // Two settled subscription invoices that sum, at cent precision, to the expected amount.
        await paymentSession.SetAsync(session.SessionId, new SubscriptionPaymentsMetadata
        {
            Payments = new Dictionary<string, PaymentInfo>
            {
                ["sub_a"] = new PaymentInfo { TransactionId = "in_a", Amount = 20.00, Status = PaymentStatus.Succeeded, Currency = Currency },
                ["sub_b"] = new PaymentInfo { TransactionId = "in_b", Amount = 10.00, Status = PaymentStatus.Succeeded, Currency = Currency },
            },
        });

        var flow = new SubscriptionFlow(session, new ContentItem());

        await handler.CompletingAsync(new SubscriptionFlowCompletingContext(flow));

        Assert.True(session.TryGet<PaymentsMetadata>(out var payments));
        Assert.True(payments.Payments.ContainsKey("in_a"));
        Assert.True(payments.Payments.ContainsKey("in_b"));
    }

    [Fact]
    public async Task CompletingAsync_WhenSubscriptionPaymentsMismatch_ThrowsPaymentValidationException()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();
        var handler = CreateHandler(paymentSession);

        var session = CreateSession(SubscriptionStep("plan", 30.00, dayDelay: 0));
        session.SessionId = "session-4";

        session.Put(new Invoice
        {
            Currency = Currency,
            FirstSubscriptionPaymentAmount = 30.00,
            DueNow = 30.00,
            GrandTotal = 30.00,
            LineItems = [],
        });

        await paymentSession.SetAsync(session.SessionId, new SubscriptionPaymentsMetadata
        {
            Payments = new Dictionary<string, PaymentInfo>
            {
                ["sub_a"] = new PaymentInfo { TransactionId = "in_a", Amount = 20.00, Status = PaymentStatus.Succeeded, Currency = Currency },
            },
        });

        var flow = new SubscriptionFlow(session, new ContentItem());

        await Assert.ThrowsAsync<PaymentValidationException>(
            () => handler.CompletingAsync(new SubscriptionFlowCompletingContext(flow)));
    }

    [Fact]
    public async Task CompletingAsync_WhenNothingIsDueNow_CompletesWithoutRequiringGatewayPayment()
    {
        var handler = CreateHandler(PaymentTestHelpers.CreatePaymentSession());

        var session = CreateSession(OneTimeStep("content", 0));
        session.SessionId = "session-5";

        session.Put(new Invoice
        {
            Currency = Currency,
            DueNow = 0,
            GrandTotal = 0,
            LineItems = [],
        });

        var flow = new SubscriptionFlow(session, new ContentItem());

        // No initial or subscription amount above the minimum, so no gateway confirmation is required.
        await handler.CompletingAsync(new SubscriptionFlowCompletingContext(flow));

        Assert.True(session.TryGet<PaymentsMetadata>(out _));
    }

    private static PaymentSubscriptionHandler CreateHandler(SubscriptionPaymentSession paymentSession)
    {
        var siteService = new Mock<ISiteService>();
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<SubscriptionSettings>())
            .Returns(new SubscriptionSettings { Currency = Currency });
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new PaymentSubscriptionHandler(
            paymentSession,
            siteService.Object,
            NullLogger<PaymentSubscriptionHandler>.Instance,
            Mock.Of<IStringLocalizer<PaymentSubscriptionHandler>>());
    }

    private static SubscriptionSession CreateSession(params SubscriptionFlowStep[] steps)
    {
        var session = new SubscriptionSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Status = SubscriptionSessionStatus.Pending,
        };

        foreach (var step in steps)
        {
            session.Steps.Add(step);
        }

        return session;
    }

    private static SubscriptionFlowStep OneTimeStep(string key, double amount)
        => new()
        {
            Key = key,
            Order = 1,
            BillingItems =
            [
                new BillingItem
                {
                    Id = key,
                    Description = key,
                    BillingAmount = amount,
                    Subscription = null,
                },
            ],
        };

    private static SubscriptionFlowStep SubscriptionStep(string key, double amount, int dayDelay)
        => new()
        {
            Key = key,
            Order = 2,
            BillingItems =
            [
                new BillingItem
                {
                    Id = key,
                    Description = key,
                    BillingAmount = amount,
                    Subscription = new SubscriptionPlan
                    {
                        BillingDuration = 1,
                        DurationType = DurationType.Month,
                        SubscriptionDayDelay = dayDelay,
                    },
                },
            ],
        };
}
