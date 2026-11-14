using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// No money is left unaccounted: what a payer pays above a balance can be kept as their customer credit, their credit
/// pays toward a balance like any other payment, and taking such a payment back moves the credit back with it.
/// </summary>
public sealed class TransactionCustomerCreditTests
{
    private static readonly DateTime _now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly PartyRef _payer = new(PartyKinds.User, "customer-1");

    [Fact]
    public async Task RecordPayment_AboveWhatIsOwed_KeepsTheRestAsCustomerCredit()
    {
        // Arrange
        var context = new CreditContext(CreateOutstanding());

        // Act
        var result = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 130.5m, CreditExcess = true }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TransactionStatus.Paid, result.Transaction.Status);
        Assert.Equal(100m, result.Event.Amount);
        Assert.Equal(30.5m, result.Event.CreditedAmount);
        Assert.Equal(30.5m, result.CreditedAmount);
        Assert.Equal(0m, result.UnappliedAmount);
        Assert.Equal(30.5m, context.Credit.Balance(_payer, "USD"));
        Assert.Equal($"Transaction:transaction-1:{result.Event.Id}", Assert.Single(context.Credit.Changes).Reference);
    }

    [Fact]
    public async Task RecordPayment_AboveWhatIsOwed_WithoutCustomerCredit_IsRefused()
    {
        // Arrange: the Store Credit feature is off.
        var context = new CreditContext(CreateOutstanding(), withCredit: false);

        // Act
        var result = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 130m, CreditExcess = true }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(TransactionStatus.Outstanding, (await context.Store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task RecordPayment_AboveWhatIsOwed_ForAGuestPayer_IsRefused()
    {
        // Arrange: a guest has no identity to keep credit for.
        var owedByGuest = CreateOutstanding();
        owedByGuest.OwnerKind = CustomerOwnerKind.Guest;
        var context = new CreditContext(owedByGuest);

        // Act
        var result = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 130m, CreditExcess = true }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(context.Credit.Changes);
    }

    [Fact]
    public async Task AnAccountPayer_KeepsTheExcessAsTheAccountsCredit_AndPaysWithIt()
    {
        // Arrange: the account owes the invoice, whoever ordered.
        var account = new PartyRef(PartyKinds.Account, "account-1");
        var owedByAccount = CreateOutstanding();
        owedByAccount.SetPayer(account);
        var context = new CreditContext(owedByAccount, CreateOutstanding("transaction-2", account));

        // Act
        var paid = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 160m, CreditExcess = true }, TestContext.Current.CancellationToken);
        var applied = await context.Service.ApplyCreditAsync("transaction-2", 60m, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(paid.Succeeded, string.Join(" ", paid.Errors));
        Assert.True(applied.Succeeded, string.Join(" ", applied.Errors));
        Assert.Equal(0m, context.Credit.Balance(account, "USD"));
        Assert.Equal(40m, applied.Transaction.OutstandingAmount);
        Assert.Equal(0m, context.Credit.Balance(new PartyRef(PartyKinds.User, "customer-1"), "USD"));
    }

    [Fact]
    public async Task ApplyCredit_PaysTowardTheBalance_AsAPayment()
    {
        // Arrange
        var context = new CreditContext(CreateOutstanding());
        context.Credit.Give(_payer, "USD", 150m);

        // Act
        var result = await context.Service.ApplyCreditAsync("transaction-1", 120m, "From the refund", TestContext.Current.CancellationToken);

        // Assert: only what is owed is spent.
        Assert.True(result.Succeeded);
        Assert.Equal(TransactionEventType.PaymentRecorded, result.Event.Type);
        Assert.Equal(TransactionsConstants.SettlementMethods.CustomerCredit, result.Event.Method);
        Assert.Equal(100m, result.Event.Amount);
        Assert.Equal(TransactionStatus.Paid, result.Transaction.Status);
        Assert.Equal(TransactionsConstants.SettlementMethods.CustomerCredit, result.Transaction.SettlementMethod);
        Assert.Equal(50m, context.Credit.Balance(_payer, "USD"));
        context.PaymentHandler.Verify(handler => handler.PaymentRecordedAsync(It.IsAny<TransactionPaymentRecordedContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyCredit_MoreThanThePayerHas_IsRefused_AndChangesNothing()
    {
        // Arrange
        var context = new CreditContext(CreateOutstanding());
        context.Credit.Give(_payer, "USD", 20m);

        // Act
        var result = await context.Service.ApplyCreditAsync("transaction-1", 40m, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(20m, context.Credit.Balance(_payer, "USD"));

        var stored = await context.Store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);
        Assert.Equal(0m, stored.AmountPaid);
        Assert.DoesNotContain(stored.Events, entry => entry.Type == TransactionEventType.PaymentRecorded);
    }

    [Fact]
    public async Task Reverse_APaymentMadeWithCredit_GivesTheCreditBack()
    {
        // Arrange
        var context = new CreditContext(CreateOutstanding());
        context.Credit.Give(_payer, "USD", 100m);
        var applied = await context.Service.ApplyCreditAsync("transaction-1", 60m, null, TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.ReverseAsync("transaction-1", applied.Event.Id, "Applied to the wrong invoice", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(100m, context.Credit.Balance(_payer, "USD"));
        Assert.Equal(0m, result.Transaction.AmountPaid);
        Assert.Equal(TransactionStatus.Outstanding, result.Transaction.Status);
    }

    [Fact]
    public async Task Reverse_APaymentWhoseExcessWasKeptAsCredit_TakesThatCreditBack()
    {
        // Arrange
        var context = new CreditContext(CreateOutstanding());
        var paid = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 125m, CreditExcess = true }, TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.ReverseAsync("transaction-1", paid.Event.Id, "The check bounced", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(0m, context.Credit.Balance(_payer, "USD"));
        Assert.Equal(100m, result.Transaction.OutstandingAmount);
    }

    [Fact]
    public async Task Reverse_APaymentWhoseCreditWasSpent_IsRefused()
    {
        // Arrange: the 25 kept as credit was spent on something else.
        var context = new CreditContext(CreateOutstanding());
        var paid = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 125m, CreditExcess = true }, TestContext.Current.CancellationToken);
        await context.Credit.SpendAsync(new CustomerCreditChange { Party = _payer, Currency = "USD", Amount = 25m, Reference = "checkout" }, TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.ReverseAsync("transaction-1", paid.Event.Id, "The check bounced", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);

        var stored = await context.Store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);
        Assert.Equal(TransactionStatus.Paid, stored.Status);
        Assert.DoesNotContain(stored.Events, entry => entry.Type == TransactionEventType.Reversed);
    }

    private static Transaction CreateOutstanding(string itemId = "transaction-1", PartyRef payer = default)
    {
        var transaction = new Transaction
        {
            ItemId = itemId,
            OwnerId = "customer-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
            Title = "Invoice INV-1001",
            Currency = "USD",
            Amount = 100m,
            TotalAmount = 100m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now.AddDays(-10),
            UpdatedUtc = _now.AddDays(-10),
        };

        if (!payer.IsEmpty)
        {
            transaction.SetPayer(payer);
        }

        return transaction;
    }

    private sealed class CreditContext
    {
        public CreditContext(Transaction transaction, bool withCredit = true)
            : this(withCredit, transaction)
        {
        }

        public CreditContext(Transaction transaction, params Transaction[] others)
            : this(true, [transaction, .. others])
        {
        }

        private CreditContext(bool withCredit, params Transaction[] transactions)
        {
            Store = new FakeTransactionStore(transactions);
            var manager = TransactionManagerFactory.Resolve(Store, new RecordingTransactionEventHandler());
            var services = new ServiceCollection();

            services.AddSingleton(PaymentHandler.Object);

            if (withCredit)
            {
                services.AddSingleton<ICustomerCreditService>(Credit);
            }

            Service = TransactionCommandServiceFactory.Create(manager, new TestClock(_now), services.BuildServiceProvider());
        }

        public FakeTransactionStore Store { get; }

        public FakeCustomerCreditService Credit { get; } = new();

        public Mock<ITransactionPaymentHandler> PaymentHandler { get; } = new();

        public DefaultTransactionCommandService Service { get; }
    }
}
