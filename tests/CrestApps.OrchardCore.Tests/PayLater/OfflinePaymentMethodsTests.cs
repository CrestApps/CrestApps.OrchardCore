using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.PayLater.Handlers;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;
using Xunit;

namespace CrestApps.OrchardCore.Tests.PayLater;

/// <summary>
/// Bank transfer and Cash on delivery are offered only when the store turned them on, and Cash on delivery only for
/// an order that is delivered; a checkout paid with either leaves an outstanding balance named after the method, due
/// when its settings say.
/// </summary>
public sealed class OfflinePaymentMethodsTests
{
    private static readonly DateTime _now = new(2026, 10, 18, 9, 0, 0, DateTimeKind.Utc);

    private BankTransferSettings _bankTransfer = new();
    private CashOnDeliverySettings _cashOnDelivery = new();
    private PayLaterSettings _payLater = new() { Enabled = true };

    [Fact]
    public async Task BankTransferNeedsToBeOnAndToHaveBankDetails()
    {
        var provider = BankTransfer();
        var session = new CheckoutSession { SessionId = "session-1" };

        Assert.False(await provider.IsAvailableAsync(session, TestContext.Current.CancellationToken));

        _bankTransfer = new BankTransferSettings { Enabled = true };
        Assert.False(await provider.IsAvailableAsync(session, TestContext.Current.CancellationToken));

        _bankTransfer = new BankTransferSettings { Enabled = true, Instructions = "Contoso Bank, account 12345678" };
        Assert.True(await provider.IsAvailableAsync(session, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CashOnDeliveryIsOfferedOnlyForAnOrderThatIsDelivered()
    {
        _cashOnDelivery = new CashOnDeliverySettings { Enabled = true };
        var provider = CashOnDelivery();
        var download = new CheckoutSession { SessionId = "session-1" };
        var delivered = new CheckoutSession { SessionId = "session-2" };
        delivered.Put(new CheckoutShippingInfo { Address = new Address { City = "Seattle", Country = "US" } });

        Assert.False(await provider.IsAvailableAsync(download, TestContext.Current.CancellationToken));
        Assert.True(await provider.IsAvailableAsync(delivered, TestContext.Current.CancellationToken));

        _cashOnDelivery = new CashOnDeliverySettings { Enabled = false };
        Assert.False(await provider.IsAvailableAsync(delivered, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PayLaterIsOffByDefault_AndCanBeTurnedOn()
    {
        var provider = PayLater();
        var session = new CheckoutSession { SessionId = "session-1" };

        _payLater = new PayLaterSettings();
        Assert.False(await provider.IsAvailableAsync(session, TestContext.Current.CancellationToken));

        _payLater = new PayLaterSettings { Enabled = true };
        Assert.True(await provider.IsAvailableAsync(session, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheSelectorKeepsOnlyAvailableProviders()
    {
        var gateway = new FakeCheckoutPaymentProvider("gateway", _ => new PaymentVerificationResult { Status = PaymentStatus.Succeeded });
        var session = new CheckoutSession { SessionId = "session-1" };

        var available = await CheckoutPaymentMethodSelector.GetAvailableAsync([gateway, BankTransfer(), CashOnDelivery(), PayLater()], session, TestContext.Current.CancellationToken);

        Assert.Equal([gateway.Key, PayLaterCheckoutPaymentProvider.ProcessorKey], available.Select(provider => provider.Key));
    }

    [Fact]
    public void OfflineMethodsDescribeThemselvesAndTakeNoMoneyAtCheckout()
    {
        foreach (var provider in new OfflineCheckoutPaymentProvider[] { BankTransfer(), CashOnDelivery() })
        {
            Assert.True(provider.Capabilities.DefersPayment);
            Assert.False(provider.Capabilities.SupportsRecurringPayments);
            Assert.False(string.IsNullOrEmpty(((ICheckoutPaymentProviderDescription)provider).Description));
        }
    }

    [Theory]
    [InlineData(BankTransferCheckoutPaymentProvider.ProcessorKey, 7)]
    [InlineData(CashOnDeliveryCheckoutPaymentProvider.ProcessorKey, null)]
    public async Task ACheckoutPaidOfflineLeavesABalanceNamedAfterTheMethod(string providerKey, int? dueInDays)
    {
        _bankTransfer = new BankTransferSettings { Enabled = true, Instructions = "Contoso Bank", NetTermDays = 7 };
        var store = new FakeTransactionStore();
        var attempt = new PaymentAttempt
        {
            SessionId = "session-1",
            ProviderKey = providerKey,
            ObligationId = "ob-1",
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 50m,
            ExpectedTaxAmount = 4m,
            Currency = "USD",
        };

        var handler = new PayLaterTransactionCheckoutHandler(
            new InMemoryPaymentAttemptStore([attempt]),
            TransactionManagerFactory.Create(store),
            [PayLater(), BankTransfer(), CashOnDelivery()],
            [OrderDescriber()],
            new TestClock(_now),
            NullLogger<PayLaterTransactionCheckoutHandler>.Instance,
            new PassThroughStringLocalizer<PayLaterTransactionCheckoutHandler>());

        var session = new CheckoutSession { SessionId = "session-1", OwnerId = "owner-1", ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = "order-1", Currency = "USD" };
        session.Steps.Add(new CheckoutFlowStep { Key = "goods", BillingItems = [new BillingItem { ItemId = "ob-1", Description = "Contoso Cap", Amount = 50m }] });

        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        var balance = Assert.Single(store.Transactions);
        Assert.Equal(providerKey, balance.Source);
        Assert.Equal(TransactionStatus.Outstanding, balance.Status);
        Assert.Equal(54m, balance.OutstandingAmount);
        Assert.Equal(dueInDays is { } days ? _now.AddDays(days) : null, balance.DueUtc);
        Assert.Equal("Order 261018-ABC123", balance.Title);
    }

    [Fact]
    public async Task AnAttemptWithAGatewayLeavesNoBalance()
    {
        var store = new FakeTransactionStore();
        var attempt = new PaymentAttempt { SessionId = "session-1", ProviderKey = "stripe", ObligationId = "ob-1", State = PaymentAttemptState.Succeeded, ExpectedAmount = 50m, Currency = "USD" };

        var handler = new PayLaterTransactionCheckoutHandler(
            new InMemoryPaymentAttemptStore([attempt]),
            TransactionManagerFactory.Create(store),
            [PayLater(), BankTransfer(), CashOnDelivery()],
            [OrderDescriber()],
            new TestClock(_now),
            NullLogger<PayLaterTransactionCheckoutHandler>.Instance,
            new PassThroughStringLocalizer<PayLaterTransactionCheckoutHandler>());

        await handler.CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(new CheckoutSession { SessionId = "session-1", ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = "order-1" })));

        Assert.Empty(store.Transactions);
    }

    private BankTransferCheckoutPaymentProvider BankTransfer()
        => new(SiteService(), Development(), new PassThroughStringLocalizer<BankTransferCheckoutPaymentProvider>());

    private CashOnDeliveryCheckoutPaymentProvider CashOnDelivery()
        => new(SiteService(), Development(), new PassThroughStringLocalizer<CashOnDeliveryCheckoutPaymentProvider>());

    private PayLaterCheckoutPaymentProvider PayLater()
        => new(SiteService(), Development(), new PassThroughStringLocalizer<PayLaterCheckoutPaymentProvider>());

    // The settings are read when asked, so a test can change them between calls.
    private ISiteService SiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<BankTransferSettings>()).Returns(() => _bankTransfer);
        site.Setup(s => s.GetOrCreate<CashOnDeliverySettings>()).Returns(() => _cashOnDelivery);
        site.Setup(s => s.GetOrCreate<PayLaterSettings>()).Returns(() => _payLater);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(() => site.Object);

        return siteService.Object;
    }

    private static ICheckoutReferenceDescriber OrderDescriber()
    {
        var describer = new Mock<ICheckoutReferenceDescriber>();
        describer
            .Setup(d => d.DescribeAsync(CheckoutReferenceTypes.Order, "order-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutReferenceDescription { Text = "Order 261018-ABC123" });

        return describer.Object;
    }

    private static IHostEnvironment Development()
        => Mock.Of<IHostEnvironment>(environment => environment.EnvironmentName == Environments.Development);
}
