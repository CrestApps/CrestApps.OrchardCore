using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The ledger commands a document such as an invoice or a credit note is built on: a payment recorded with how and when
/// it was received, an adjustment that names its document and is recorded once, and an entry taken back by a reversing
/// entry instead of being edited.
/// </summary>
public sealed class TransactionLedgerCommandsTests
{
    private static readonly DateTime _now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecordPaymentAsync_RecordsHowAndWhenTheMoneyWasReceived()
    {
        // Arrange
        var context = new LedgerContext(CreateOutstanding());
        var received = _now.AddDays(-3);

        // Act
        var result = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest
        {
            Amount = 40m,
            PaymentMethod = " check ",
            ReceivedUtc = received,
            Reference = "1042",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TransactionEventType.PaymentRecorded, result.Event.Type);
        Assert.Equal("check", result.Event.PaymentMethod);
        Assert.Equal(received, result.Event.ReceivedUtc);
        Assert.Equal(_now, result.Event.CreatedUtc);
        Assert.Equal("1042", result.Event.Reference);
        Assert.Equal(TransactionsConstants.SettlementMethods.Offline, result.Event.Method);
        Assert.Equal(0m, result.UnappliedAmount);
        Assert.Equal(TransactionStatus.PartiallyPaid, result.Transaction.Status);
    }

    [Fact]
    public async Task RecordPaymentAsync_ForMoreThanIsOwed_ReturnsWhatWasNotRecorded()
    {
        // Arrange
        var context = new LedgerContext(CreateOutstanding());

        // Act
        var result = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 130.5m }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(100m, result.Event.Amount);
        Assert.Equal(30.5m, result.UnappliedAmount);
        Assert.Equal(TransactionStatus.Paid, result.Transaction.Status);
    }

    [Fact]
    public async Task RecordPaymentAsync_ReceivedInTheFuture_IsRefused()
    {
        // Arrange
        var context = new LedgerContext(CreateOutstanding());

        // Act
        var result = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 10m, ReceivedUtc = _now.AddDays(1) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(0m, result.Transaction.AmountPaid);
    }

    [Fact]
    public async Task AdjustAsync_ForADocument_NamesItOnTheTimeline_AndIsRecordedOnce()
    {
        // Arrange
        var context = new LedgerContext(CreateOutstanding());
        var request = new TransactionAdjustmentRequest
        {
            Amount = 25m,
            Kind = TransactionAdjustmentKind.Credit,
            Reason = "Damaged item",
            DocumentKind = "CreditNote",
            DocumentId = "credit-1",
            DocumentNumber = "CN-1001",
        };

        // Act
        var first = await context.Service.AdjustAsync("transaction-1", request, TestContext.Current.CancellationToken);
        var second = await context.Service.AdjustAsync("transaction-1", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal("CreditNote", first.Event.DocumentKind);
        Assert.Equal("credit-1", first.Event.DocumentId);
        Assert.Equal("CN-1001", first.Event.DocumentNumber);
        Assert.Same(first.Event, second.Event);
        Assert.Equal(25m, second.Transaction.AmountAdjusted);
        Assert.Single(second.Transaction.Events, entry => entry.Type == TransactionEventType.Adjusted);
    }

    [Fact]
    public async Task AdjustAsync_ThatLeavesNothingOwed_SettlesTheTransaction()
    {
        // Arrange
        var context = new LedgerContext(CreateOutstanding());

        // Act
        var result = await context.Service.AdjustAsync("transaction-1", new TransactionAdjustmentRequest
        {
            Amount = 100m,
            Kind = TransactionAdjustmentKind.Credit,
            DocumentKind = "CreditNote",
            DocumentId = "credit-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TransactionStatus.Paid, result.Transaction.Status);
        Assert.Equal(TransactionsConstants.SettlementMethods.Adjusted, result.Transaction.SettlementMethod);
    }

    [Fact]
    public async Task ReverseAsync_OfAPaymentRecordedByHand_OwesItAgain_AndKeepsBothEntries()
    {
        // Arrange
        var context = new LedgerContext(CreateOutstanding());
        var payment = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 100m }, TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Paid, payment.Transaction.Status);

        // Act
        var result = await context.Service.ReverseAsync("transaction-1", payment.Event.Id, "Recorded on the wrong customer", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(TransactionEventType.Reversed, result.Event.Type);
        Assert.Equal(payment.Event.Id, result.Event.ReversesEventId);
        Assert.Equal(100m, result.Event.Amount);
        Assert.Equal(0m, result.Transaction.AmountPaid);
        Assert.Equal(100m, result.Transaction.OutstandingAmount);
        Assert.Equal(TransactionStatus.Outstanding, result.Transaction.Status);
        Assert.Null(result.Transaction.SettledUtc);
        Assert.Contains(result.Transaction.Events, entry => entry.Id == payment.Event.Id && entry.Type == TransactionEventType.PaymentRecorded);
    }

    [Fact]
    public async Task ReverseAsync_OfOneOfTwoPayments_LeavesTheTransactionPartlyPaid()
    {
        // Arrange
        var context = new LedgerContext(CreateOutstanding());

        await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 60m }, TestContext.Current.CancellationToken);

        var second = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 40m }, TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.ReverseAsync("transaction-1", second.Event.Id, "Check bounced", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(60m, result.Transaction.AmountPaid);
        Assert.Equal(TransactionStatus.PartiallyPaid, result.Transaction.Status);
    }

    [Fact]
    public async Task ReverseAsync_OfAnAdjustment_OwesItAgain_AndLetsTheDocumentBeRecordedAgain()
    {
        // Arrange
        var context = new LedgerContext(CreateOutstanding());
        var request = new TransactionAdjustmentRequest { Amount = 100m, Kind = TransactionAdjustmentKind.WriteOff, DocumentKind = "Invoice", DocumentId = "invoice-1" };
        var writeOff = await context.Service.AdjustAsync("transaction-1", request, TestContext.Current.CancellationToken);

        // Act
        var reversed = await context.Service.ReverseAsync("transaction-1", writeOff.Event.Id, "The customer will pay after all", TestContext.Current.CancellationToken);

        // The commands return the same transaction, so its status is read before the next command changes it.
        var statusAfterReversal = reversed.Transaction.Status;
        var again = await context.Service.AdjustAsync("transaction-1", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(reversed.Succeeded);
        Assert.Equal(TransactionStatus.Outstanding, statusAfterReversal);
        Assert.True(again.Succeeded);
        Assert.NotSame(writeOff.Event, again.Event);
        Assert.Equal(100m, again.Transaction.AmountAdjusted);
    }

    [Fact]
    public async Task ReverseAsync_Twice_IsRefusedTheSecondTime()
    {
        // Arrange
        var context = new LedgerContext(CreateOutstanding());
        var payment = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 40m }, TestContext.Current.CancellationToken);

        await context.Service.ReverseAsync("transaction-1", payment.Event.Id, "Mistake", TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.ReverseAsync("transaction-1", payment.Event.Id, "Mistake", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(0m, result.Transaction.AmountPaid);
    }

    [Fact]
    public async Task ReverseAsync_OfAPaymentTakenOnline_IsRefused()
    {
        // Arrange
        var transaction = CreateOutstanding();

        transaction.AmountPaid = 100m;
        transaction.Status = TransactionStatus.Paid;
        transaction.Events.Add(new TransactionEvent
        {
            Id = "online-1",
            Type = TransactionEventType.PaymentRecorded,
            Amount = 100m,
            Method = TransactionsConstants.SettlementMethods.Online,
            PaymentAttemptId = "attempt-1",
        });

        var context = new LedgerContext(transaction);

        // Act
        var result = await context.Service.ReverseAsync("transaction-1", "online-1", "Mistake", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(100m, result.Transaction.AmountPaid);
    }

    [Fact]
    public async Task ReverseAsync_WithoutAReason_IsRefused()
    {
        // Arrange
        var context = new LedgerContext(CreateOutstanding());
        var payment = await context.Service.RecordPaymentAsync("transaction-1", new TransactionPaymentRequest { Amount = 40m }, TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.ReverseAsync("transaction-1", payment.Event.Id, "  ", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(40m, result.Transaction.AmountPaid);
    }

    [Fact]
    public async Task ASavedTransaction_HasAnIdOnEveryTimelineEntry()
    {
        // Arrange
        var transaction = CreateOutstanding();

        transaction.Events.Add(new TransactionEvent { Type = TransactionEventType.Note, Message = "Added by another module without an id." });

        var context = new LedgerContext(transaction);

        // Act
        var result = await context.Service.CancelAsync("transaction-1", "No longer needed", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.All(result.Transaction.Events, entry => Assert.False(string.IsNullOrEmpty(entry.Id)));
        Assert.Equal(result.Transaction.Events.Count, result.Transaction.Events.Select(entry => entry.Id).Distinct().Count());
    }

    private static Transaction CreateOutstanding()
        => new()
        {
            ItemId = "transaction-1",
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

    private sealed class LedgerContext
    {
        public LedgerContext(params Transaction[] transactions)
        {
            var store = new FakeTransactionStore(transactions);
            var manager = TransactionManagerFactory.Resolve(store, new RecordingTransactionEventHandler());
            var services = new ServiceCollection();

            services.AddSingleton(Mock.Of<ITransactionPaymentHandler>());

            Service = TransactionCommandServiceFactory.Create(manager, new TestClock(_now), services.BuildServiceProvider());
        }

        public DefaultTransactionCommandService Service { get; }
    }
}
