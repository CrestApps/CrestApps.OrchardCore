using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Core.Models;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Endpoints;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Exercises the Pay Later processing pipeline against the real, in-memory payment session so that the
/// offline commitment it records is guaranteed to satisfy the payment completion handler. This is the
/// exact contract the checkout relies on: the endpoint records the payments, the flow submits, and
/// <see cref="PaymentSubscriptionHandler.CompletingAsync"/> validates them.
/// </summary>
public class CreatePayLaterEndpointTests
{
    private const string Currency = "USD";

    [Fact]
    public async Task ProcessAsync_RecordsCommitmentThatSatisfiesCompletion()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();
        var handler = CreateHandler(paymentSession);

        var session = await CreatePendingSessionWithInvoiceAsync(handler, price: 30.00, initialAmount: null);

        Assert.True(session.TryGet<Invoice>(out var invoice));

        await CreatePayLaterEndpoint.ProcessAsync(
            session.SessionId,
            session,
            invoice,
            FixedClock(),
            paymentSession,
            Mock.Of<ISubscriptionSessionStore>(),
            DevelopmentEnvironment());

        // The offline commitment must be recorded under both payment purposes so completion can validate it.
        var initialInfo = await paymentSession.GetInitialPaymentInfoAsync(session.SessionId);
        Assert.NotNull(initialInfo);
        Assert.Equal(SubscriptionConstants.PayLaterProcessorKey, initialInfo.GatewayId);

        var subscriptionInfo = await paymentSession.GetSubscriptionPaymentInfoAsync(session.SessionId);
        Assert.NotNull(subscriptionInfo);
        Assert.Equal(30.00, subscriptionInfo.Payments.Values.Sum(x => x.Amount), 2);
        Assert.All(subscriptionInfo.Payments.Values, p => Assert.Equal(PaymentStatus.Succeeded, p.Status));

        // The session must carry the resulting subscription metadata attributed to the Pay Later gateway.
        Assert.True(session.TryGet<SubscriptionsMetadata>(out var metadata));
        var subscription = Assert.Single(metadata.Subscriptions);
        Assert.Equal(SubscriptionConstants.PayLaterProcessorKey, subscription.Gateway);
        Assert.Equal(GatewayMode.Testing, subscription.GatewayMode);

        // Completion must succeed with the recorded commitment and must not require an external gateway.
        var flow = new SubscriptionFlow(session, new ContentItem());
        await handler.CompletingAsync(new SubscriptionFlowCompletingContext(flow));

        Assert.True(session.TryGet<PaymentsMetadata>(out var payments));
        Assert.NotEmpty(payments.Payments);
    }

    [Fact]
    public async Task ProcessAsync_WithSetupFeeAndRecurring_RecordsInitialAndSubscriptionPayments()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();
        var handler = CreateHandler(paymentSession);

        var session = await CreatePendingSessionWithInvoiceAsync(handler, price: 9.99, initialAmount: 50.00);

        Assert.True(session.TryGet<Invoice>(out var invoice));

        await CreatePayLaterEndpoint.ProcessAsync(
            session.SessionId,
            session,
            invoice,
            FixedClock(),
            paymentSession,
            Mock.Of<ISubscriptionSessionStore>(),
            DevelopmentEnvironment());

        var initialInfo = await paymentSession.GetInitialPaymentInfoAsync(session.SessionId);
        Assert.NotNull(initialInfo);
        Assert.Equal(50.00, initialInfo.Amount);

        var subscriptionInfo = await paymentSession.GetSubscriptionPaymentInfoAsync(session.SessionId);
        Assert.Equal(9.99, subscriptionInfo.Payments.Values.Sum(x => x.Amount), 2);

        var flow = new SubscriptionFlow(session, new ContentItem());
        await handler.CompletingAsync(new SubscriptionFlowCompletingContext(flow));

        Assert.True(session.TryGet<PaymentsMetadata>(out var payments));
        // One initial payment and one subscription payment.
        Assert.Equal(2, payments.Payments.Count);
    }

    [Fact]
    public async Task ProcessAsync_WhenBillingDurationIsInvalid_Throws()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();

        var session = new SubscriptionSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Status = SubscriptionSessionStatus.Pending,
        };

        // A misconfigured plan with a non-positive billing duration cannot produce a billing date. The
        // endpoint relies on this throwing so it can surface an actionable error instead of a stuck UI.
        var invoice = new Invoice
        {
            Currency = Currency,
            FirstSubscriptionPaymentAmount = 10.00,
            DueNow = 10.00,
            GrandTotal = 10.00,
            LineItems =
            [
                new InvoiceLineItem
                {
                    Id = "plan",
                    Description = "plan",
                    Quantity = 1,
                    UnitPrice = 10.00,
                    Subscription = new SubscriptionPlan
                    {
                        BillingDuration = 0,
                        DurationType = DurationType.Month,
                    },
                },
            ],
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreatePayLaterEndpoint.ProcessAsync(
            session.SessionId,
            session,
            invoice,
            FixedClock(),
            paymentSession,
            Mock.Of<ISubscriptionSessionStore>(),
            DevelopmentEnvironment()));
    }

    private static async Task<SubscriptionSession> CreatePendingSessionWithInvoiceAsync(
        PaymentSubscriptionHandler handler,
        double price,
        double? initialAmount)
    {
        var contentItem = new ContentItem { ContentType = "Plan" };
        contentItem.Weld(new ProductPart { Price = price });
        contentItem.Weld(new SubscriptionPart
        {
            BillingDuration = 1,
            DurationType = DurationType.Month,
            InitialAmount = initialAmount,
            InitialAmountDescription = initialAmount.HasValue ? "setup" : null,
        });

        var session = new SubscriptionSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Status = SubscriptionSessionStatus.Pending,
            ContentItemVersionId = "plan-version-1",
        };

        await handler.ActivatingAsync(new SubscriptionFlowActivatingContext(session, contentItem));

        var flow = new SubscriptionFlow(session, contentItem);

        await handler.ActivatedAsync(new SubscriptionFlowActivatedContext(flow));

        return session;
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
            new NullSubscriptionTaxService(),
            NullLogger<PaymentSubscriptionHandler>.Instance,
            Mock.Of<IStringLocalizer<PaymentSubscriptionHandler>>());
    }

    private static IClock FixedClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        return clock.Object;
    }

    private static IHostEnvironment DevelopmentEnvironment()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        return environment.Object;
    }
}
