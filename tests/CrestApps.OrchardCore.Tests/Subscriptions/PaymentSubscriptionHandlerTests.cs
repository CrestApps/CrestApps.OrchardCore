using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Exceptions;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
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
            OneTimeStep("content", 19.99m),
            SubscriptionStep("plan", 10.00m, dayDelay: 0));

        var flow = new SubscriptionFlow(session, new ContentItem());

        await handler.ActivatedAsync(new SubscriptionFlowActivatedContext(flow));

        Assert.True(session.TryGet<Invoice>(out var invoice));
        Assert.Equal(Currency, invoice.Currency);
        Assert.Equal(19.99m, invoice.InitialPaymentAmount);
        Assert.Equal(10.00m, invoice.FirstSubscriptionPaymentAmount);
        Assert.Equal(29.99m, invoice.DueNow);
        Assert.Equal(29.99m, invoice.GrandTotal);
        Assert.Equal(2, invoice.LineItems.Length);
    }

    [Fact]
    public async Task ActivatedAsync_UsesProductOwnedCurrency_OverSiteSetting()
    {
        // Arrange
        var snapshotResolver = new Mock<IProductSnapshotResolver>();
        snapshotResolver
            .Setup(r => r.ResolveAsync(It.IsAny<ProductSnapshotContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SellableProduct { Currency = "CAD" });

        var handler = CreateHandler(PaymentTestHelpers.CreatePaymentSession(), snapshotResolver.Object);

        var session = CreateSession(
            OneTimeStep("content", 19.99m),
            SubscriptionStep("plan", 10.00m, dayDelay: 0));

        var flow = new SubscriptionFlow(session, new ContentItem());

        // Act
        await handler.ActivatedAsync(new SubscriptionFlowActivatedContext(flow));

        // Assert
        Assert.True(session.TryGet<Invoice>(out var invoice));
        Assert.Equal("CAD", invoice.Currency);
    }

    [Fact]
    public async Task ActivatingAsync_WithProductAndSubscriptionParts_AddsPaymentStepWithPlanBilling()
    {
        var handler = CreateHandler(PaymentTestHelpers.CreatePaymentSession());

        var contentItem = CreatePlanContentItem(price: 9.99m, initialAmount: 50, initialDescription: "setup");

        var session = new SubscriptionSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Status = SubscriptionSessionStatus.Pending,
            ContentItemVersionId = "plan-version-1",
        };

        await handler.ActivatingAsync(new SubscriptionFlowActivatingContext(session, contentItem));

        var paymentStep = Assert.Single(session.Steps, s => s.Key == SubscriptionConstants.StepKey.Payment);

        Assert.NotNull(paymentStep.BillingItems);
        Assert.Equal(2, paymentStep.BillingItems.Length);

        var recurring = Assert.Single(paymentStep.BillingItems, b => b.Subscription != null);
        Assert.Equal(9.99m, recurring.BillingAmount);
        Assert.Equal("plan-version-1", recurring.ItemId);

        var setupFee = Assert.Single(paymentStep.BillingItems, b => b.Subscription == null);
        Assert.Equal(50, setupFee.BillingAmount);
        Assert.Equal("setup", setupFee.Description);
        Assert.Equal("plan-version-1" + SubscriptionConstants.InitialFeeIdPrefix, setupFee.ItemId);
    }

    [Fact]
    public async Task ActivatingAsync_WithoutProductPart_AddsPaymentStepWithoutBilling()
    {
        var handler = CreateHandler(PaymentTestHelpers.CreatePaymentSession());

        // A subscription content item that does not expose a price produces no plan billing.
        var contentItem = new ContentItem { ContentType = "Plan" };
        contentItem.Weld(new SubscriptionPart { BillingDuration = 1, DurationType = DurationType.Month });

        var session = new SubscriptionSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Status = SubscriptionSessionStatus.Pending,
            ContentItemVersionId = "plan-version-2",
        };

        await handler.ActivatingAsync(new SubscriptionFlowActivatingContext(session, contentItem));

        var paymentStep = Assert.Single(session.Steps, s => s.Key == SubscriptionConstants.StepKey.Payment);

        Assert.Null(paymentStep.BillingItems);
    }

    [Fact]
    public async Task ActivatingThenActivated_PlainPlan_ProducesInvoiceForRecurringAndSetupFee()
    {
        // Reproduces a plain subscription plan (ProductPart + SubscriptionPart only, no associated
        // content types and no tenant onboarding). The invoice must charge the recurring price plus
        // the one-time setup fee rather than resolving to a $0.00 invoice.
        var handler = CreateHandler(PaymentTestHelpers.CreatePaymentSession());

        var contentItem = CreatePlanContentItem(price: 9.99m, initialAmount: 50, initialDescription: "setup");
        contentItem.DisplayText = "Plan A 9.99/month + 50 setup fee";

        var session = new SubscriptionSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Status = SubscriptionSessionStatus.Pending,
            ContentItemVersionId = "plan-version-3",
        };

        await handler.ActivatingAsync(new SubscriptionFlowActivatingContext(session, contentItem));

        var flow = new SubscriptionFlow(session, contentItem);

        await handler.ActivatedAsync(new SubscriptionFlowActivatedContext(flow));

        Assert.True(session.TryGet<Invoice>(out var invoice));
        Assert.Equal(50, invoice.InitialPaymentAmount);
        Assert.Equal(9.99m, invoice.FirstSubscriptionPaymentAmount);
        Assert.Equal(59.99m, invoice.DueNow);
        Assert.Equal(59.99m, invoice.GrandTotal);
        Assert.Equal(2, invoice.LineItems.Length);
    }

    [Fact]
    public async Task ActivatedAsync_DelayedSubscription_IsNotDueNow()
    {
        var handler = CreateHandler(PaymentTestHelpers.CreatePaymentSession());

        var session = CreateSession(
            OneTimeStep("content", 25.00m),
            SubscriptionStep("plan", 10.00m, dayDelay: 30));

        var flow = new SubscriptionFlow(session, new ContentItem());

        await handler.ActivatedAsync(new SubscriptionFlowActivatedContext(flow));

        Assert.True(session.TryGet<Invoice>(out var invoice));
        Assert.Equal(25.00m, invoice.InitialPaymentAmount);
        // A delayed subscription is not collected on the first payment.
        Assert.Null(invoice.FirstSubscriptionPaymentAmount);
        Assert.Equal(25.00m, invoice.DueNow);
    }

    [Fact]
    public async Task CompletingAsync_WhenInitialPaymentMatches_StoresPaymentAndDoesNotThrow()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();
        var handler = CreateHandler(paymentSession);

        var session = CreateSession(OneTimeStep("content", 19.99m));
        session.SessionId = "session-1";

        session.Put(new Invoice
        {
            Currency = Currency,
            InitialPaymentAmount = 19.99m,
            DueNow = 19.99m,
            GrandTotal = 19.99m,
            LineItems = [],
        });

        // The payment gateway reported an amount that is only equal to the invoice at cent precision.
        await paymentSession.SetAsync(session.SessionId, new InitialPaymentMetadata
        {
            TransactionId = "pi_1",
            Amount = 10.00m + 9.99m,
            Currency = Currency,
            GatewayId = "stripe",
        });

        var flow = new SubscriptionFlow(session, new ContentItem());

        await handler.CompletingAsync(new SubscriptionFlowCompletingContext(flow));

        Assert.True(session.TryGet<PaymentsMetadata>(out var payments));
        Assert.True(payments.Payments.ContainsKey("pi_1"));
        Assert.Equal(PaymentStatus.Succeeded, payments.Payments["pi_1"].Status);
        Assert.Equal(19.99m, payments.Payments["pi_1"].Amount, 2);
    }

    [Fact]
    public async Task CompletingAsync_WhenInitialPaymentAmountMismatches_ThrowsPaymentValidationException()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();
        var handler = CreateHandler(paymentSession);

        var session = CreateSession(OneTimeStep("content", 19.99m));
        session.SessionId = "session-2";

        session.Put(new Invoice
        {
            Currency = Currency,
            InitialPaymentAmount = 19.99m,
            DueNow = 19.99m,
            GrandTotal = 19.99m,
            LineItems = [],
        });

        await paymentSession.SetAsync(session.SessionId, new InitialPaymentMetadata
        {
            TransactionId = "pi_2",
            Amount = 18.00m,
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

        var session = CreateSession(SubscriptionStep("plan", 30.00m, dayDelay: 0));
        session.SessionId = "session-3";

        session.Put(new Invoice
        {
            Currency = Currency,
            FirstSubscriptionPaymentAmount = 30.00m,
            DueNow = 30.00m,
            GrandTotal = 30.00m,
            LineItems = [],
        });

        // Two settled subscription invoices that sum, at cent precision, to the expected amount.
        await paymentSession.SetAsync(session.SessionId, new SubscriptionPaymentsMetadata
        {
            Payments = new Dictionary<string, PaymentInfo>
            {
                ["sub_a"] = new PaymentInfo { TransactionId = "in_a", Amount = 20.00m, Status = PaymentStatus.Succeeded, Currency = Currency },
                ["sub_b"] = new PaymentInfo { TransactionId = "in_b", Amount = 10.00m, Status = PaymentStatus.Succeeded, Currency = Currency },
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

        var session = CreateSession(SubscriptionStep("plan", 30.00m, dayDelay: 0));
        session.SessionId = "session-4";

        session.Put(new Invoice
        {
            Currency = Currency,
            FirstSubscriptionPaymentAmount = 30.00m,
            DueNow = 30.00m,
            GrandTotal = 30.00m,
            LineItems = [],
        });

        await paymentSession.SetAsync(session.SessionId, new SubscriptionPaymentsMetadata
        {
            Payments = new Dictionary<string, PaymentInfo>
            {
                ["sub_a"] = new PaymentInfo { TransactionId = "in_a", Amount = 20.00m, Status = PaymentStatus.Succeeded, Currency = Currency },
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
        => CreateHandler(paymentSession, PaymentTestHelpers.CreateProductSnapshotResolver());

    private static PaymentSubscriptionHandler CreateHandler(
        SubscriptionPaymentSession paymentSession,
        IProductSnapshotResolver snapshotResolver)
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
            snapshotResolver,
            NullLogger<PaymentSubscriptionHandler>.Instance,
            Mock.Of<IStringLocalizer<PaymentSubscriptionHandler>>());
    }

    private static ContentItem CreatePlanContentItem(decimal price, decimal? initialAmount, string initialDescription)
    {
        var contentItem = new ContentItem { ContentType = "Plan" };

        contentItem.Weld(new ProductPart { Price = price });
        contentItem.Weld(new SubscriptionPart
        {
            BillingDuration = 1,
            DurationType = DurationType.Month,
            InitialAmount = initialAmount,
            InitialAmountDescription = initialDescription,
        });

        return contentItem;
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

    private static SubscriptionFlowStep OneTimeStep(string key, decimal amount)
        => new()
        {
            Key = key,
            Order = 1,
            BillingItems =
            [
                new BillingItem
                {
                    ItemId = key,
                    Description = key,
                    BillingAmount = amount,
                    Subscription = null,
                },
            ],
        };

    private static SubscriptionFlowStep SubscriptionStep(string key, decimal amount, int dayDelay)
        => new()
        {
            Key = key,
            Order = 2,
            BillingItems =
            [
                new BillingItem
                {
                    ItemId = key,
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
