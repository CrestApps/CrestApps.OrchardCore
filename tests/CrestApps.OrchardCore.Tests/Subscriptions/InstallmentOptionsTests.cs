using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Transactions;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// The "Choose how to pay" step only offers installments the plan service will accept, only for balances that can be
/// split, and only when a card can be kept for the later payments. These pin what is offered and when the step appears.
/// </summary>
public sealed class InstallmentOptionsTests
{
    private const string ProviderKey = "card";
    private const string Source = "club-fees";

    [Fact]
    public async Task Resolver_OffersTheOptionsOfTheProvidersForTheTransactionsSource()
    {
        // Arrange
        var resolver = CreateResolver(
            new FixedProvider(Source, Option("3-Monthly", 3)),
            new FixedProvider("other-source", Option("6-Monthly", 6)),
            new FixedProvider(null, Option("2-Monthly", 2)));

        // Act
        var options = await resolver.GetOptionsAsync(CreateTransaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new[] { "3-Monthly", "2-Monthly" }, options.Select(option => option.Id));
    }

    [Fact]
    public async Task Resolver_DropsOptionsThePlanServiceWouldRefuse()
    {
        // Arrange
        var resolver = CreateResolver(new FixedProvider(
            Source,
            Option("ok", 3),
            Option("ok", 4),
            Option(SubscriptionConstants.InstallmentPlans.PayInFullOptionId, 3),
            Option(string.Empty, 3),
            Option("none", 0),
            Option("too-many", 121),
            new InstallmentOption { Id = "down-too-big", InstallmentCount = 3, DownPaymentAmount = 300m },
            new InstallmentOption { Id = "negative", InstallmentCount = 3, DownPaymentAmount = -1m },
            null));

        // Act
        var options = await resolver.GetOptionsAsync(CreateTransaction(), TestContext.Current.CancellationToken);

        // Assert
        var only = Assert.Single(options);

        Assert.Equal("ok", only.Id);
        Assert.Equal(3, only.InstallmentCount);
    }

    [Fact]
    public async Task Resolver_WhenAProviderFails_StillOffersTheOthers()
    {
        // Arrange
        var resolver = CreateResolver(new ThrowingProvider(), new FixedProvider(Source, Option("3-Monthly", 3)));

        // Act
        var options = await resolver.GetOptionsAsync(CreateTransaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("3-Monthly", Assert.Single(options).Id);
    }

    [Fact]
    public async Task Resolver_WithoutAProviderThatCanKeepACard_OffersNothing()
    {
        // Arrange
        var resolver = CreateResolver(canKeepCards: false, new FixedProvider(Source, Option("3-Monthly", 3)));

        // Act
        var options = await resolver.GetOptionsAsync(CreateTransaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(options);
    }

    [Theory]
    [InlineData(TransactionStatus.Paid, 0, CustomerOwnerKind.Authenticated)]
    [InlineData(TransactionStatus.Outstanding, 10, CustomerOwnerKind.Authenticated)]
    [InlineData(TransactionStatus.Outstanding, 0, CustomerOwnerKind.Guest)]
    public async Task Resolver_ForABalanceThatCannotBeSplit_OffersNothing(TransactionStatus status, int amountPaid, CustomerOwnerKind ownerKind)
    {
        // Arrange
        var resolver = CreateResolver(new FixedProvider(Source, Option("3-Monthly", 3)));
        var transaction = CreateTransaction();
        transaction.Status = status;
        transaction.AmountPaid = amountPaid;
        transaction.OwnerKind = ownerKind;

        // Act
        var options = await resolver.GetOptionsAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(options);
    }

    [Fact]
    public async Task Resolver_FindsOnlyAnOptionThatIsOffered()
    {
        // Arrange
        var resolver = CreateResolver(new FixedProvider(Source, Option("3-Monthly", 3)));
        var transaction = CreateTransaction();

        // Act
        var found = await resolver.FindOptionAsync(transaction, "3-Monthly", TestContext.Current.CancellationToken);
        var forged = await resolver.FindOptionAsync(transaction, "12-Monthly", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(found);
        Assert.Null(forged);
    }

    [Fact]
    public async Task SiteSettingsProvider_OffersTheConfiguredCountsForTheConfiguredSources()
    {
        // Arrange
        var provider = new SiteSettingsInstallmentOptionProvider(SiteServiceFactory.Create(new InstallmentPlanSettings
        {
            CheckoutSources = [Source],
            CheckoutInstallmentCounts = [6, 3, 3],
            CheckoutFrequency = InstallmentFrequency.Monthly,
        }));

        // Act
        var options = (await provider.GetOptionsAsync(CreateTransaction(), TestContext.Current.CancellationToken)).ToArray();

        // Assert
        Assert.Null(provider.Source);
        Assert.Equal(new[] { 3, 6 }, options.Select(option => option.InstallmentCount));
        Assert.All(options, option =>
        {
            Assert.Equal(InstallmentFrequency.Monthly, option.Frequency);
            Assert.Equal(0m, option.DownPaymentAmount);
        });
        Assert.Equal(2, options.Select(option => option.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("other-source", 0)]
    [InlineData(Source, 500)]
    public async Task SiteSettingsProvider_OutsideTheConfiguredSourcesOrBelowTheMinimum_OffersNothing(string configuredSource, int minimum)
    {
        // Arrange
        var provider = new SiteSettingsInstallmentOptionProvider(SiteServiceFactory.Create(new InstallmentPlanSettings
        {
            CheckoutSources = [configuredSource],
            CheckoutInstallmentCounts = [3],
            CheckoutMinimumAmount = minimum,
        }));

        // Act
        var options = await provider.GetOptionsAsync(CreateTransaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(options);
    }

    [Fact]
    public async Task CheckoutHandler_WhenInstallmentsAreOffered_AddsTheChooseHowToPayStepBeforeThePayment()
    {
        // Arrange
        var (handler, transaction) = await CreateHandlerAsync(new FixedProvider(Source, Option("3-Monthly", 3)));
        var session = CreateCheckout(transaction);

        // Act
        await handler.ActivatingAsync(new CheckoutFlowActivatingContext(session));

        // Assert
        var step = Assert.Single(session.Steps, candidate => candidate.Key == SubscriptionConstants.InstallmentPlans.CheckoutStepKey);

        Assert.True(step.CollectData);
        Assert.Equal("Choose how to pay", step.Title);
        Assert.True(step.Order < session.Steps.Single(candidate => candidate.Key == CheckoutConstants.PaymentStepKey).Order);
        Assert.Equal(SubscriptionConstants.InstallmentPlans.CheckoutStepKey, new CheckoutFlow(session).GetFirstStep().Key);
    }

    [Fact]
    public async Task CheckoutHandler_WhenNothingIsOffered_LeavesTheCheckoutAsItWas()
    {
        // Arrange
        var (handler, transaction) = await CreateHandlerAsync();
        var session = CreateCheckout(transaction);

        // Act
        await handler.ActivatingAsync(new CheckoutFlowActivatingContext(session));

        // Assert
        Assert.DoesNotContain(session.Steps, candidate => candidate.Key == SubscriptionConstants.InstallmentPlans.CheckoutStepKey);
    }

    [Fact]
    public async Task CheckoutHandler_ForACheckoutThatIsNotForATransaction_AddsNothing()
    {
        // Arrange
        var (handler, transaction) = await CreateHandlerAsync(new FixedProvider(Source, Option("3-Monthly", 3)));
        var session = CreateCheckout(transaction);
        session.ReferenceType = "SubscriptionPlan";

        // Act
        await handler.ActivatingAsync(new CheckoutFlowActivatingContext(session));

        // Assert
        Assert.DoesNotContain(session.Steps, candidate => candidate.Key == SubscriptionConstants.InstallmentPlans.CheckoutStepKey);
    }

    [Theory]
    [InlineData("3, 6", new[] { 3, 6 })]
    [InlineData("6;3 3", new[] { 3, 6 })]
    [InlineData("", new int[0])]
    public void SettingsDriver_ReadsTheNumbersOfPaymentsOffered(string value, int[] expected)
    {
        Assert.True(InstallmentPlanSettingsDisplayDriver.TryParseCheckoutCounts(value, out var counts));
        Assert.Equal(expected, counts);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("121")]
    [InlineData("three")]
    [InlineData("2, 3, 4, 5, 6, 7, 8")]
    public void SettingsDriver_RefusesNumbersOfPaymentsThatCannotBeOffered(string value)
        => Assert.False(InstallmentPlanSettingsDisplayDriver.TryParseCheckoutCounts(value, out _));

    private static async Task<(InstallmentOptionsCheckoutHandler Handler, Transaction Transaction)> CreateHandlerAsync(params IInstallmentOptionProvider[] providers)
    {
        var store = new FakeTransactionStore();
        var transaction = CreateTransaction();

        await store.CreateAsync(transaction, TestContext.Current.CancellationToken);

        var handler = new InstallmentOptionsCheckoutHandler(
            TransactionManagerFactory.Create(store),
            CreateResolver(providers),
            new PassThroughStringLocalizer<InstallmentOptionsCheckoutHandler>());

        return (handler, transaction);
    }

    private static CheckoutSession CreateCheckout(Transaction transaction)
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = transaction.ItemId,
            Status = CheckoutSessionStatus.Pending,
            Currency = transaction.Currency,
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = TransactionsConstants.SettlementStepKey,
            Order = 0,
            Conceal = true,
        });

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = CheckoutConstants.PaymentStepKey,
            Order = int.MaxValue,
        });

        return session;
    }

    private static InstallmentOptionResolver CreateResolver(params IInstallmentOptionProvider[] providers)
        => CreateResolver(canKeepCards: true, providers);

    private static InstallmentOptionResolver CreateResolver(bool canKeepCards, params IInstallmentOptionProvider[] providers)
    {
        var provider = new Mock<ICheckoutPaymentProvider>();
        provider.SetupGet(candidate => candidate.Key).Returns(ProviderKey);
        provider.SetupGet(candidate => candidate.Capabilities).Returns(new PaymentProviderCapabilities
        {
            SupportsOneTimePayments = true,
            SupportsEmbeddedElements = true,
            SupportsSavedPaymentMethods = canKeepCards,
        });

        var providerResolver = new Mock<ICheckoutPaymentProviderResolver>();
        providerResolver.Setup(resolver => resolver.GetProviders()).Returns([provider.Object]);

        var savedProvider = new Mock<ICheckoutSavedPaymentMethodProvider>();
        savedProvider.SetupGet(candidate => candidate.Key).Returns(ProviderKey);

        return new InstallmentOptionResolver(
            providers,
            providerResolver.Object,
            [savedProvider.Object],
            NullLogger<InstallmentOptionResolver>.Instance);
    }

    private static Transaction CreateTransaction()
        => new()
        {
            ItemId = "tx-1",
            Title = "Season fee",
            Source = Source,
            OwnerId = "customer-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
            Currency = "USD",
            Amount = 300m,
            TotalAmount = 300m,
            Status = TransactionStatus.Outstanding,
        };

    private static InstallmentOption Option(string id, int count)
        => new()
        {
            Id = id,
            InstallmentCount = count,
            Frequency = InstallmentFrequency.Monthly,
        };

    private sealed class FixedProvider : IInstallmentOptionProvider
    {
        private readonly InstallmentOption[] _options;

        public FixedProvider(string source, params InstallmentOption[] options)
        {
            Source = source;
            _options = options;
        }

        public string Source { get; }

        public Task<IEnumerable<InstallmentOption>> GetOptionsAsync(Transaction transaction, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<InstallmentOption>>(_options);
    }

    private sealed class ThrowingProvider : IInstallmentOptionProvider
    {
        public string Source
            => null;

        public Task<IEnumerable<InstallmentOption>> GetOptionsAsync(Transaction transaction, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The provider is broken.");
    }
}
