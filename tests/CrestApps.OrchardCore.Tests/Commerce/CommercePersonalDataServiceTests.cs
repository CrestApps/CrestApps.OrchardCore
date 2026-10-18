using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Customers;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Exports and anonymizes one person's commerce data across every module that keeps some: the checkouts they went
/// through as a guest and with their account, the payments, refunds and coupon taken in them, their transactions,
/// subscription, installment plan and Stripe customers. The person's details go; what the business must keep stays.
/// </summary>
public sealed class CommercePersonalDataServiceTests : IAsyncLifetime
{
    private const string Email = "jane@example.test";
    private const string Name = "Jane Doe";
    private const string UserId = "user-1";

    private static readonly DateTime _now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    // Nothing is escaped, so a search of the serialized record sees an email or a name exactly as it is stored.
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly InMemoryCheckoutSessionStore _sessions = new();
    private readonly List<CheckoutSession> _seededSessions = [];
    private readonly InMemoryPaymentAttemptStore _attempts = new();
    private readonly InMemoryPaymentRefundStore _refunds = new();
    private readonly FakeTransactionStore _transactions = new();
    private readonly InMemorySubscriptionStore _subscriptions = new();
    private readonly InMemoryInstallmentPlanStore _plans = new();
    private readonly Mock<IStripeCustomerService> _stripe = new();

    public ValueTask InitializeAsync()
        => SeedAsync();

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    [Fact]
    public async Task ExportAsync_IncludesTransactionsPaymentsRefundsAndPlans()
    {
        // Arrange
        var service = CreateService();

        // Act
        var export = await service.ExportAsync(CustomerOwner.ForUser(UserId), Email, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now, export.ExportedUtc);
        Assert.Equal(new[] { "t-guest", "t-plan" }, export.Transactions.Select(item => item.ItemId).Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "INV-1001", "INV-1002" }, export.Transactions.Select(item => item.InvoiceNumber).Order(StringComparer.Ordinal));
        Assert.Contains(export.Transactions, item => item.ReceiptNumbers.Contains("R-1001"));
        Assert.Equal(new[] { "a-guest", "a-plan", "a-settlement" }, export.Payments.Select(item => item.ItemId).Order(StringComparer.Ordinal));
        Assert.Equal(90m, export.Payments.Single(item => item.ItemId == "a-guest").Amount);
        Assert.Equal("r-guest", Assert.Single(export.Refunds).ItemId);
        Assert.Equal(20m, export.Refunds[0].Amount);

        var plan = Assert.Single(export.InstallmentPlans);
        Assert.Equal("p-1", plan.ItemId);
        Assert.Equal(Name, plan.CustomerName);
        Assert.Equal("Visa •••• 4242", plan.PaymentMethod);
        Assert.Equal(new[] { "t-plan" }, plan.TransactionIds);

        Assert.Equal("sub-guest", Assert.Single(export.Subscriptions).ItemId);
        Assert.Equal(new[] { "s-account", "s-guest", "s-settlement" }, export.CheckoutSessions.Select(item => item.SessionId).Order(StringComparer.Ordinal));

        var guestSession = export.CheckoutSessions.Single(item => item.SessionId == "s-guest");
        Assert.Equal("203.0.113.7", guestSession.IPAddress);
        Assert.Equal("Test Browser", guestSession.UserAgent);
        Assert.Equal(Email, guestSession.SavedSteps["Contact"]?["Email"]?.GetValue<string>());

        var redemption = Assert.Single(export.CouponRedemptions);
        Assert.Equal("SAVE10", redemption.Code);
        Assert.Equal(10m, redemption.Amount);

        Assert.Equal(new[] { "cus_checkout", "cus_plan", "cus_subscription" }, export.ProviderCustomers.Select(item => item.CustomerReference).Order(StringComparer.Ordinal));
        Assert.Empty(export.Warnings);
    }

    [Fact]
    public async Task ExportAsync_ByEmailOnly_FindsTheGuestRecordsThroughTheirCheckouts()
    {
        // Arrange
        var service = CreateService();

        // Act
        var export = await service.ExportAsync(owner: null, "JANE@example.TEST", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(export.Transactions, item => item.ItemId == "t-guest");
        Assert.Contains(export.Subscriptions, item => item.ItemId == "sub-guest");
        Assert.Contains(export.InstallmentPlans, item => item.ItemId == "p-1");
        Assert.DoesNotContain(export.Transactions, item => item.ItemId == "t-other");
        Assert.DoesNotContain(export.CheckoutSessions, item => item.SessionId == "s-other");
    }

    [Fact]
    public async Task AnonymizeAsync_LeavesNoRecordWithTheEmailOrName()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.AnonymizeAsync(CustomerOwner.ForUser(UserId), Email, "Deleted customer", TestContext.Current.CancellationToken);

        // Assert
        foreach (var record in await GetPersonsRecordsAsync())
        {
            Assert.DoesNotContain(Email, record, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Name, record, StringComparison.OrdinalIgnoreCase);
        }

        var guestSession = _seededSessions.Single(session => session.SessionId == "s-guest");
        Assert.Null(guestSession.IPAddress);
        Assert.Null(guestSession.AgentInfo);
        Assert.Empty(guestSession.SavedSteps);
        Assert.True(guestSession.TryGet<CheckoutContactInfo>(out var contact));
        Assert.Equal("Deleted customer", contact.DisplayName);
        Assert.Null(contact.Email);
        Assert.Null(contact.Phone);
        Assert.Equal("US", contact.BillingAddress.Country);
        Assert.Null(contact.BillingAddress.AddressLine1);

        var plan = Assert.Single(_plans.Plans);
        Assert.Equal("Deleted customer", plan.CustomerName);
        Assert.Null(plan.CustomerEmail);
        Assert.Null(plan.Notes);
        Assert.Null(plan.PaymentMethod.Last4);
        Assert.Equal("cus_plan", plan.PaymentMethod.CustomerReference);

        Assert.Equal(3, result.CheckoutSessions);
        Assert.Equal(2, result.Transactions);
        Assert.Equal(1, result.Subscriptions);
        Assert.Equal(1, result.InstallmentPlans);
        Assert.Equal(1, result.CouponRedemptions);
        Assert.Equal(1, result.Refunds);
        Assert.Equal(3, result.ProviderCustomers);
        Assert.Empty(result.Warnings);

        _stripe.Verify(stripe => stripe.UpdateAsync(
            It.IsIn("cus_checkout", "cus_plan", "cus_subscription"),
            It.Is<UpdateCustomerRequest>(request => request.Name == "Deleted customer" && request.Email == string.Empty && request.Phone == string.Empty)), Times.Exactly(3));
        _stripe.Verify(stripe => stripe.UpdateAsync("cus_elsewhere", It.IsAny<UpdateCustomerRequest>()), Times.Never);
    }

    [Fact]
    public async Task AnonymizeAsync_KeepsLedgerTotalsAndInvoiceAndReceiptNumbers()
    {
        // Arrange
        var service = CreateService();
        var before = LedgerSnapshot();

        // Act
        await service.AnonymizeAsync(CustomerOwner.ForUser(UserId), Email, replacementText: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(before, LedgerSnapshot());
        Assert.Equal(90m + 250m + 40m, _transactions.Transactions.Sum(transaction => transaction.TotalAmount));
        Assert.Equal("INV-1001", _transactions.Transactions.Single(transaction => transaction.ItemId == "t-guest").InvoiceNumber);
        Assert.Equal("R-1001", _transactions.Transactions.Single(transaction => transaction.ItemId == "t-guest").Events.Single(item => item.ReceiptNumber is not null).ReceiptNumber);
        Assert.Equal("Installment plan for ", _transactions.Transactions.Single(transaction => transaction.ItemId == "t-plan").Title);
    }

    [Fact]
    public async Task AnonymizeAsync_LeavesSomeoneElsesRecordsAlone()
    {
        // Arrange
        var service = CreateService();
        var other = _transactions.Transactions.Single(transaction => transaction.ItemId == "t-other");
        var otherSession = _seededSessions.Single(session => session.SessionId == "s-other");

        // Act
        await service.AnonymizeAsync(CustomerOwner.ForUser(UserId), Email, "Deleted customer", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Sam Other", other.GuestContactName);
        Assert.Equal("sam@example.test", other.GuestContactEmail);
        Assert.Equal("198.51.100.1", otherSession.IPAddress);
        Assert.True(otherSession.TryGet<CheckoutContactInfo>(out var contact));
        Assert.Equal("sam@example.test", contact.Email);
    }

    [Fact]
    public async Task ExportAsync_SkipsTheModulesThatAreNotEnabled()
    {
        // Arrange
        var service = new DefaultCommercePersonalDataService(
            [new TransactionPersonalDataContributor(TransactionManagerFactory.Create(_transactions))],
            Clock());

        // Act
        var export = await service.ExportAsync(CustomerOwner.ForUser(UserId), Email, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("t-plan", Assert.Single(export.Transactions).ItemId);
        Assert.Empty(export.CheckoutSessions);
        Assert.Empty(export.Payments);
        Assert.Empty(export.InstallmentPlans);
        Assert.Empty(export.ProviderCustomers);
    }

    [Fact]
    public async Task ExportAsync_WithNeitherOwnerNorEmail_Throws()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExportAsync(owner: null, " ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StripeContributor_WhenStripeIsNotConfigured_LeavesStripeAlone()
    {
        // Arrange
        var service = CreateService(stripeApiKey: null);

        // Act
        var result = await service.AnonymizeAsync(CustomerOwner.ForUser(UserId), Email, "Deleted customer", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.ProviderCustomers);
        _stripe.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ErasingACustomer_AnonymizesTheirCommerceDataAndExportingIncludesIt()
    {
        // Arrange
        var customers = new InMemoryCustomerStore();
        var customer = new Customer
        {
            ItemId = "c1",
            UserId = UserId,
            DisplayName = Name,
            Email = Email,
        };

        await customers.CreateAsync(customer, TestContext.Current.CancellationToken);

        var privacy = new DefaultCustomerPrivacyService(customers, [new CommercePersonalDataCustomerPrivacyHandler(CreateService())], Clock());

        // Act
        var export = await privacy.ExportAsync(customer, "admin", TestContext.Current.CancellationToken);
        await privacy.EraseAsync(customer, "admin", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, export["commerce"]?["transactions"]?.AsArray().Count);
        Assert.Single(export["commerce"]?["installmentPlans"]?.AsArray());

        foreach (var record in await GetPersonsRecordsAsync())
        {
            Assert.DoesNotContain(Email, record, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Name, record, StringComparison.OrdinalIgnoreCase);
        }
    }

    private DefaultCommercePersonalDataService CreateService(string stripeApiKey = "sk_test_personal_data")
    {
        var services = new ServiceCollection()
            .AddSingleton(_stripe.Object)
            .BuildServiceProvider();

        var transactionManager = TransactionManagerFactory.Create(_transactions);

        return new DefaultCommercePersonalDataService(
            [
                new CheckoutPersonalDataContributor(_sessions, _attempts, _refunds, Clock()),
                new CouponPersonalDataContributor(_sessions, Mock.Of<ICouponRedemptionService>()),
                new TransactionPersonalDataContributor(transactionManager),
                new TransactionSettlementPersonalDataContributor(transactionManager, _attempts),
                new SubscriptionPersonalDataContributor(new SubscriptionManager(_subscriptions, [], NullLogger<CatalogManager<Subscription>>.Instance)),
                new InstallmentPlanPersonalDataContributor(_plans),
                new StripePersonalDataContributor(
                    Options.Create(new StripeOptions { ApiKey = stripeApiKey }),
                    services,
                    NullLogger<StripePersonalDataContributor>.Instance),
            ],
            Clock());
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private async ValueTask SeedAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Jane checked out once as a guest, with a coupon, and her guest checkout started a subscription.
        var guest = AddSession("s-guest", ownerId: null, Name, "Jane@Example.test", "203.0.113.7");
        guest.SavedSteps["Contact"] = new JsonObject
        {
            ["Name"] = Name,
            ["Email"] = Email,
        };
        guest.Put(new CheckoutInvoice
        {
            Currency = "USD",
            Discounts =
            [
                new DiscountLine
                {
                    ProviderKey = CouponDiscountProvider.ProviderKey,
                    Code = "SAVE10",
                    Description = "Ten off",
                    Amount = 10m,
                },
            ],
        });

        // She then signed up, and paid her plan's down payment with her account.
        AddSession("s-account", UserId, Name, Email, ipAddress: null);

        // A pay link she opened later settled what her guest checkout left owing, without a contact on it.
        AddSession("s-settlement", ownerId: null, displayName: null, email: null, "203.0.113.8", referenceType: "Transaction", referenceId: "t-guest");

        // Someone else entirely.
        AddSession("s-other", ownerId: null, "Sam Other", "sam@example.test", "198.51.100.1");

        await _attempts.CreateAsync(new PaymentAttempt
        {
            ItemId = "a-guest",
            SessionId = "s-guest",
            ProviderKey = "Stripe",
            State = PaymentAttemptState.Succeeded,
            Currency = "USD",
            ExpectedAmount = 90m,
            ConfirmedAmount = 90m,
            CreatedUtc = _now,
        }, cancellationToken);

        await _attempts.CreateAsync(new PaymentAttempt
        {
            ItemId = "a-plan",
            SessionId = "s-account",
            ProviderKey = "Stripe",
            State = PaymentAttemptState.Failed,
            Currency = "USD",
            ExpectedAmount = 50m,
            FailureReason = "The card of Jane Doe was declined.",
            CreatedUtc = _now,
        }, cancellationToken);

        await _attempts.CreateAsync(new PaymentAttempt
        {
            ItemId = "a-settlement",
            SessionId = "s-settlement",
            ReferenceType = "Transaction",
            ReferenceId = "t-guest",
            ProviderKey = "Stripe",
            State = PaymentAttemptState.Succeeded,
            Currency = "USD",
            ExpectedAmount = 30m,
            ConfirmedAmount = 30m,
            CreatedUtc = _now,
        }, cancellationToken);

        await _refunds.CreateAsync(new PaymentRefund
        {
            ItemId = "r-guest",
            SessionId = "s-guest",
            OriginalAttemptId = "a-guest",
            ProviderKey = "Stripe",
            Currency = "USD",
            RefundGrossAmount = 20m,
            Status = RefundStatus.Succeeded,
            Reason = "Requested by Jane Doe",
            CreatedUtc = _now,
        }, cancellationToken);

        await _transactions.CreateAsync(new Transaction
        {
            ItemId = "t-guest",
            Title = "Order 1001",
            OwnerId = "s-guest",
            OwnerKind = CustomerOwnerKind.Guest,
            GuestContactName = Name,
            GuestContactEmail = Email,
            CheckoutSessionId = "s-guest",
            Currency = "USD",
            Amount = 90m,
            TotalAmount = 90m,
            AmountPaid = 90m,
            AmountRefunded = 20m,
            Status = TransactionStatus.Paid,
            InvoiceNumber = "INV-1001",
            CreatedUtc = _now.AddDays(-3),
            Events =
            [
                new TransactionEvent
                {
                    Type = TransactionEventType.PaymentRecorded,
                    Message = $"Payment received from {Email}.",
                    ReceiptNumber = "R-1001",
                    Amount = 90m,
                },
            ],
        }, cancellationToken);

        await _transactions.CreateAsync(new Transaction
        {
            ItemId = "t-plan",
            Title = $"Installment plan for {Name}",
            OwnerId = UserId,
            OwnerKind = CustomerOwnerKind.Authenticated,
            Currency = "USD",
            Amount = 250m,
            TotalAmount = 250m,
            AmountPaid = 0m,
            Status = TransactionStatus.Outstanding,
            InvoiceNumber = "INV-1002",
            CreatedUtc = _now.AddDays(-2),
            AutoCollection = new TransactionAutoCollection
            {
                ProviderKey = "Stripe",
                PaymentMethodDescription = "Visa •••• 4242",
            },
        }, cancellationToken);

        await _transactions.CreateAsync(new Transaction
        {
            ItemId = "t-other",
            Title = "Order 1002",
            OwnerId = "s-other",
            OwnerKind = CustomerOwnerKind.Guest,
            GuestContactName = "Sam Other",
            GuestContactEmail = "sam@example.test",
            CheckoutSessionId = "s-other",
            Currency = "USD",
            Amount = 40m,
            TotalAmount = 40m,
            Status = TransactionStatus.Outstanding,
            InvoiceNumber = "INV-1003",
            CreatedUtc = _now.AddDays(-1),
        }, cancellationToken);

        await _subscriptions.CreateAsync(new Subscription
        {
            ItemId = "sub-guest",
            Title = "Club membership",
            OwnerId = "s-guest",
            OwnerKind = CustomerOwnerKind.Guest,
            GuestContactName = Name,
            GuestContactEmail = Email,
            CheckoutSessionId = "s-guest",
            ProviderKey = StripeConstants.ProcessorKey,
            ProviderCustomerId = "cus_subscription",
            Status = SubscriptionStatus.Active,
            Currency = "USD",
            Amount = 15m,
            CreatedUtc = _now.AddDays(-3),
        }, cancellationToken);

        await _plans.CreateAsync(new InstallmentPlan
        {
            ItemId = "p-1",
            Title = "Season pass",
            OwnerId = UserId,
            CustomerName = Name,
            CustomerEmail = Email,
            Currency = "USD",
            TotalAmount = 300m,
            DownPaymentAmount = 50m,
            InstallmentCount = 1,
            Status = InstallmentPlanStatus.Active,
            Notes = $"Call {Name} before charging.",
            DownPaymentSessionId = "s-account",
            PaymentMethod = new SavedPaymentMethod
            {
                ProviderKey = StripeConstants.ProcessorKey,
                CustomerReference = "cus_plan",
                PaymentMethodReference = "pm_1",
                Brand = "visa",
                Last4 = "4242",
                ExpirationMonth = 12,
                ExpirationYear = 2030,
            },
            Payments =
            [
                new InstallmentPlanPayment
                {
                    Number = 0,
                    Amount = 50m,
                    TransactionId = "t-plan",
                    CheckoutSessionIds = ["s-account"],
                },
            ],
            CreatedUtc = _now.AddDays(-2),
        }, cancellationToken);

        _stripe
            .Setup(service => service.GetAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new CustomerResponse { Id = id, Name = Name, Email = Email });

        _stripe
            .Setup(service => service.ListByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new CustomerResponse[]
            {
                new CustomerResponse
                {
                    Id = "cus_checkout",
                    Name = Name,
                    Email = Email,
                    Metadata = new Dictionary<string, string> { [StripePersonalDataContributor.CheckoutSessionMetadataKey] = "s-guest" },
                },

                // A customer another system made in the same Stripe account is not the store's to redact.
                new CustomerResponse
                {
                    Id = "cus_elsewhere",
                    Name = Name,
                    Email = Email,
                },
            });

        _stripe
            .Setup(service => service.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateCustomerRequest>()))
            .ReturnsAsync((string id, UpdateCustomerRequest _) => new UpdateCustomerResponse { Updated = true, CustomerId = id });
    }

    private CheckoutSession AddSession(
        string sessionId,
        string ownerId,
        string displayName,
        string email,
        string ipAddress,
        string referenceType = "Order",
        string referenceId = "order-1")
    {
        var session = new CheckoutSession
        {
            SessionId = sessionId,
            OwnerId = ownerId,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Currency = "USD",
            Status = CheckoutSessionStatus.Completed,
            CreatedUtc = _now.AddDays(-3),
            ModifiedUtc = _now.AddDays(-3),
            CompletedUtc = _now.AddDays(-3),
            IPAddress = ipAddress,
            AgentInfo = ipAddress is null ? null : "Test Browser",
        };

        if (email is not null)
        {
            session.Put(new CheckoutContactInfo
            {
                DisplayName = displayName,
                Email = email,
                Phone = "+1 555 0100",
                BillingAddress = new Address
                {
                    Name = displayName,
                    AddressLine1 = "1 Main St",
                    City = "Springfield",
                    Region = "IL",
                    Country = "US",
                },
            });
        }

        _sessions.Seed(session);
        _seededSessions.Add(session);

        return session;
    }

    // Every record that belonged to Jane, serialized, so a test can look for her details anywhere in it.
    private async Task<IReadOnlyList<string>> GetPersonsRecordsAsync()
    {
        var records = new List<string>();

        records.AddRange(_seededSessions.Where(session => session.SessionId != "s-other").Select(Serialize));
        records.AddRange((await _attempts.GetAllAsync(TestContext.Current.CancellationToken)).Select(Serialize));
        records.AddRange((await _refunds.GetAllAsync(TestContext.Current.CancellationToken)).Select(Serialize));
        records.AddRange(_transactions.Transactions.Where(transaction => transaction.ItemId != "t-other").Select(Serialize));
        records.AddRange(_subscriptions.Subscriptions.Select(Serialize));
        records.AddRange(_plans.Plans.Select(Serialize));

        return records;
    }

    private string LedgerSnapshot()
    {
        var transactions = _transactions.Transactions
            .OrderBy(transaction => transaction.ItemId, StringComparer.Ordinal)
            .Select(transaction => new
            {
                transaction.ItemId,
                transaction.Status,
                transaction.Currency,
                transaction.Amount,
                transaction.TaxAmount,
                transaction.TotalAmount,
                transaction.AmountPaid,
                transaction.AmountRefunded,
                transaction.CreatedUtc,
                transaction.SettledUtc,
                transaction.InvoiceNumber,
                Receipts = transaction.Events.Select(item => item.ReceiptNumber).ToArray(),
            });

        var plan = _plans.Plans.Select(item => new { item.ItemId, item.Status, item.TotalAmount, item.DownPaymentAmount, item.AmountPaid });
        var subscriptions = _subscriptions.Subscriptions.Select(item => new { item.ItemId, item.Status, item.Amount, item.ProviderCustomerId });
        var sessions = _seededSessions.Select(item => new { item.SessionId, item.Status, item.CreatedUtc, item.CompletedUtc, Discount = item.TryGet<CheckoutInvoice>(out var invoice) ? invoice.DiscountTotal : 0m });

        return JsonSerializer.Serialize(new { transactions, plan, subscriptions, sessions });
    }

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, _serializerOptions);
}
