using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Models;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A deferred balance goes on one outstanding invoice per checkout, however often the checkout completes, and a refund
/// of it is waived off what is still owed.
/// </summary>
public sealed class DeferredBalanceTenderProviderBaseTests
{
    private static readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<Transaction> _transactions = [];
    private readonly List<(string TransactionId, decimal Amount)> _waived = [];

    [Fact]
    public async Task ADebit_OpensOneOutstandingInvoice_EvenWhenTheCheckoutCompletesTwice()
    {
        var tender = CreateTender();
        var flow = new CheckoutFlow(new CheckoutSession { SessionId = "s1", OwnerId = "u1", ReferenceType = "Order", ReferenceId = "o1" });

        await tender.DebitAsync("s1|later", 70m, "USD", flow, TestContext.Current.CancellationToken);
        await tender.DebitAsync("s1|later", 70m, "USD", flow, TestContext.Current.CancellationToken);

        var invoice = Assert.Single(_transactions);

        Assert.Equal(("Pay later", "Test", "s1", "test-later"), (invoice.Title, invoice.Source, invoice.CheckoutSessionId, invoice.ObligationId));
        Assert.Equal((70m, 0m, TransactionStatus.Outstanding, "USD"), (invoice.TotalAmount, invoice.AmountPaid, invoice.Status, invoice.Currency));
        Assert.Equal(("Account", "a1"), (invoice.ParentReferenceType, invoice.ParentReferenceId));
        Assert.Equal(_now.AddDays(30), invoice.DueUtc);
        Assert.Equal(1, tender.Debited);
    }

    [Fact]
    public async Task ADebit_ForAnotherCheckout_IsRefused()
    {
        var tender = CreateTender();
        var flow = new CheckoutFlow(new CheckoutSession { SessionId = "s2", OwnerId = "u1" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => tender.DebitAsync("s1|later", 70m, "USD", flow, TestContext.Current.CancellationToken));
        Assert.Empty(_transactions);
    }

    [Fact]
    public async Task ACredit_IsWaivedOffWhatIsStillOwed()
    {
        var tender = CreateTender();
        var flow = new CheckoutFlow(new CheckoutSession { SessionId = "s1", OwnerId = "u1" });

        await tender.DebitAsync("s1|later", 70m, "USD", flow, TestContext.Current.CancellationToken);
        _transactions[0].AmountPaid = 50m;

        await tender.CreditAsync("s1|later", 30m, "USD", reason: null, TestContext.Current.CancellationToken);

        // Only the 20 still owed comes off; the rest was paid and is refunded by the store.
        Assert.Equal([(_transactions[0].ItemId, 20m)], _waived);
        Assert.Equal(20m, tender.Credited);
    }

    private TestTender CreateTender()
    {
        var manager = new Mock<ITransactionManager>();
        manager.Setup(value => value.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Transaction { ItemId = "t" + (_transactions.Count + 1) });
        manager.Setup(value => value.CreateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Callback((Transaction transaction, CancellationToken _) => _transactions.Add(transaction))
            .Returns(ValueTask.CompletedTask);
        manager.Setup(value => value.GetByObligationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string sessionId, string obligationId, CancellationToken _) => _transactions.FirstOrDefault(transaction => transaction.CheckoutSessionId == sessionId && transaction.ObligationId == obligationId));

        var commands = new Mock<ITransactionCommandService>();
        commands.Setup(value => value.AdjustAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<TransactionAdjustmentKind>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string id, decimal amount, TransactionAdjustmentKind _, string _, CancellationToken _) => _waived.Add((id, amount)))
            .ReturnsAsync(new TransactionCommandResult());

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return new TestTender(manager.Object, commands.Object, clock.Object);
    }

    private sealed class TestTender : DeferredBalanceTenderProviderBase
    {
        private readonly IClock _clock;

        public TestTender(ITransactionManager transactions, ITransactionCommandService commands, IClock clock)
            : base(transactions, commands, clock, NullLogger.Instance, new PassThroughStringLocalizer<TestTender>())
        {
            _clock = clock;
        }

        public int Debited { get; private set; }

        public decimal Credited { get; private set; }

        public override string Key
            => "test";

        protected override string ObligationId
            => "test-later";

        public override Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TenderLine>>([]);

        public override Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
            => Task.FromResult<decimal?>(null);

        protected override string GetSessionId(string reference)
            => reference?.Split('|')[0];

        protected override Task<DeferredBalanceDebit> PrepareDebitAsync(string reference, CheckoutSession session, decimal amount, string currency, CancellationToken cancellationToken)
            => Task.FromResult(new DeferredBalanceDebit
            {
                Title = "Pay later",
                Source = "Test",
                ParentReferenceType = "Account",
                ParentReferenceId = "a1",
                DueUtc = _clock.UtcNow.AddDays(30),
                Message = "Owed in 30 days.",
            });

        protected override Task DebitedAsync(Transaction transaction, DeferredBalanceDebit debit, CheckoutSession session, CancellationToken cancellationToken)
        {
            Debited++;

            return Task.CompletedTask;
        }

        protected override Task CreditedAsync(string reference, Transaction transaction, decimal adjustment, CancellationToken cancellationToken)
        {
            Credited += adjustment;

            return Task.CompletedTask;
        }
    }
}
