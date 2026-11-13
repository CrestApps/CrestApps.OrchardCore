using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.Workflows;
using CrestApps.OrchardCore.Webhooks;
using Moq;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The ledger had no automation: nothing outside it learned that a debt was recorded, paid, settled, late, canceled or
/// refunded. Each change now starts its workflow event and publishes its webhook.
/// </summary>
public sealed class TransactionWorkflowsTests
{
    [Fact]
    public async Task Created_StartsTheCreatedEvent_WithTheTransactionAsInput()
    {
        // Arrange
        var (handler, triggered) = CreateWorkflowHandler();

        // Act
        await handler.CreatedAsync(CreateTransaction(), TestContext.Current.CancellationToken);

        // Assert
        var (name, input, correlationId) = Assert.Single(triggered);
        Assert.Equal(nameof(TransactionCreatedEvent), name);
        Assert.Equal("tx-1", correlationId);
        Assert.Equal("tx-1", input["TransactionId"]);
        Assert.Equal(120m, input["OutstandingAmount"]);
        Assert.Equal("Account", input["PayerKind"]);
        Assert.Equal("acct-1", input["PayerId"]);
    }

    [Fact]
    public async Task StatusChanged_ToPaid_StartsTheSettledEvent_AndNoOtherChangeDoes()
    {
        // Arrange
        var (handler, triggered) = CreateWorkflowHandler();
        var transaction = CreateTransaction();

        // Act
        await handler.StatusChangedAsync(transaction, TransactionStatus.Pending, TransactionStatus.Outstanding, TestContext.Current.CancellationToken);
        await handler.StatusChangedAsync(transaction, TransactionStatus.Outstanding, TransactionStatus.PartiallyPaid, TestContext.Current.CancellationToken);
        await handler.StatusChangedAsync(transaction, TransactionStatus.PartiallyPaid, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(TransactionSettledEvent), Assert.Single(triggered).Name);
    }

    [Fact]
    public async Task PaymentRecorded_StartsThePaymentEvent_WithTheAmountPaid()
    {
        // Arrange
        var (handler, triggered) = CreateWorkflowHandler();
        var payment = new TransactionEvent { Id = "evt-1", Type = TransactionEventType.PaymentRecorded, Amount = 50m, Reference = "CHK-7" };

        // Act
        await handler.PaymentRecordedAsync(new TransactionPaymentRecordedContext(CreateTransaction(), payment), TestContext.Current.CancellationToken);

        // Assert
        var (name, input, _) = Assert.Single(triggered);
        Assert.Equal(nameof(TransactionPaymentRecordedEvent), name);
        Assert.Equal(50m, input["Amount"]);
        Assert.Equal("CHK-7", input["Reference"]);
        Assert.Equal("evt-1", input["EventId"]);
    }

    [Fact]
    public async Task CanceledOverdueAndRefunded_EachStartTheirEvent()
    {
        // Arrange
        var (handler, triggered) = CreateWorkflowHandler();
        var transaction = CreateTransaction();

        // Act
        await handler.CanceledAsync(transaction, "duplicate", TestContext.Current.CancellationToken);
        await handler.BecameOverdueAsync(transaction, TestContext.Current.CancellationToken);
        await handler.RefundedAsync(transaction, new TransactionEvent { Id = "refund-1", Amount = 10m }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([nameof(TransactionCanceledEvent), nameof(TransactionOverdueEvent), nameof(TransactionRefundedEvent)], triggered.Select(entry => entry.Name));
        Assert.Equal("duplicate", triggered[0].Input["Reason"]);
    }

    [Theory]
    [InlineData(null, null, "PayLater", "Invoice", true)]
    [InlineData("paylater, bank", null, "PayLater", "Invoice", true)]
    [InlineData("bank", null, "PayLater", "Invoice", false)]
    [InlineData(null, "Order", "PayLater", "Invoice", false)]
    [InlineData(null, "order,invoice", "PayLater", "Invoice", true)]
    public void EventFilters_MatchTheSourceAndTheReferenceType(string sources, string referenceTypes, string source, string referenceType, bool expected)
    {
        // Arrange
        var activity = new TransactionCreatedEvent(new PassThroughStringLocalizer<TransactionCreatedEvent>())
        {
            Sources = sources,
            ReferenceTypes = referenceTypes,
        };

        var context = new WorkflowExecutionContext(
            new WorkflowType { WorkflowTypeId = "t1" },
            new Workflow { WorkflowId = "w1" },
            new Dictionary<string, object>(StringComparer.Ordinal) { ["Source"] = source, ["ReferenceType"] = referenceType },
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);

        // Act
        var result = activity.CanExecute(context, null);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Webhooks_ArePublishedWithStableEventIds()
    {
        // Arrange
        var published = new List<(string Name, string EventId)>();
        var publisher = new Mock<IWebhookPublisher>();
        publisher
            .Setup(candidate => candidate.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string name, object _, string eventId, CancellationToken _) => published.Add((name, eventId)))
            .Returns(Task.CompletedTask);

        var handler = new TransactionWebhookEventHandler(publisher.Object);
        var transaction = CreateTransaction();
        var payment = new TransactionEvent { Id = "evt-1", Amount = 50m };

        // Act
        await handler.CreatedAsync(transaction, TestContext.Current.CancellationToken);
        await handler.PaymentRecordedAsync(new TransactionPaymentRecordedContext(transaction, payment), TestContext.Current.CancellationToken);
        await handler.StatusChangedAsync(transaction, TransactionStatus.Outstanding, TransactionStatus.PartiallyPaid, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                (TransactionsConstants.WebhookEvents.Created, "evt_transaction-created_tx-1"),
                (TransactionsConstants.WebhookEvents.PaymentRecorded, "evt_transaction-payment_tx-1_evt-1"),
            ],
            published);
    }

    private static (TransactionWorkflowEventHandler Handler, List<(string Name, IDictionary<string, object> Input, string CorrelationId)> Triggered) CreateWorkflowHandler()
    {
        var triggered = new List<(string Name, IDictionary<string, object> Input, string CorrelationId)>();
        var manager = new Mock<IWorkflowManager>();
        manager
            .Setup(candidate => candidate.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Callback((string name, IDictionary<string, object> input, string correlationId, bool _, bool _) => triggered.Add((name, input, correlationId)))
            .ReturnsAsync([]);

        return (new TransactionWorkflowEventHandler(manager.Object), triggered);
    }

    private static Transaction CreateTransaction()
        => new()
        {
            ItemId = "tx-1",
            Title = "Invoice INV-1001",
            Source = "PayLater",
            Currency = "USD",
            Amount = 100m,
            TaxAmount = 20m,
            TotalAmount = 120m,
            PayerKind = "Account",
            PayerId = "acct-1",
            ReferenceType = "Invoice",
            ReferenceId = "inv-1",
            Status = TransactionStatus.Outstanding,
        };
}
