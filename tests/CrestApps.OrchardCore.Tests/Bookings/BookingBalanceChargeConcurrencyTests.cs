using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Bookings;

/// <summary>
/// SEC-C12: a booking balance is charged once, however many requests ask for it at the same time (a double click, or a
/// click while the background task runs), and a copy of a balance read before it was paid is never charged again.
/// </summary>
public sealed class BookingBalanceChargeConcurrencyTests
{
    private readonly BookingBalance _stored = new()
    {
        ItemId = "balance-1",
        OrderId = "order-1",
        OrderNumber = "1001",
        TransactionId = "transaction-1",
        DepositSessionId = "deposit-1",
        Amount = 80m,
        Currency = "USD",
        Status = BookingBalanceStatus.Scheduled,
    };

    private readonly Mock<ICheckoutEngine> _engine = new();
    private readonly List<BeginPaymentOptions> _begun = [];
    private Func<Task> _gate = () => Task.CompletedTask;

    public BookingBalanceChargeConcurrencyTests()
    {
        var sessions = 0;

        _engine.Setup(engine => engine.StartAsync(It.IsAny<StartCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new CheckoutSession { SessionId = "session-" + Interlocked.Increment(ref sessions) });
        _engine.Setup(engine => engine.BeginPaymentAsync(It.IsAny<string>(), It.IsAny<BeginPaymentOptions>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, BeginPaymentOptions options, CancellationToken _) =>
            {
                lock (_begun)
                {
                    _begun.Add(options);
                }

                await _gate();

                return new PaymentBeginOutcome { Succeeded = true };
            });
        _engine.Setup(engine => engine.TryCompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Completed });
    }

    [Fact]
    public async Task ChargeAsync_TwoAtOnce_BeginOnePayment()
    {
        // Arrange
        var charger = CreateCharger();
        var started = new TaskCompletionSource();
        var finish = new TaskCompletionSource();

        _gate = async () =>
        {
            if (_begun.Count == 1)
            {
                started.TrySetResult();
                await finish.Task;
            }
        };

        // Act
        var first = charger.ChargeAsync(Copy(), TestContext.Current.CancellationToken);

        await started.Task;
        await charger.ChargeAsync(Copy(), TestContext.Current.CancellationToken);

        finish.SetResult();
        await first;

        // Assert
        var begun = Assert.Single(_begun);

        // The provider's key comes from the balance and its attempt, so a charge that got past the lock still collapses.
        Assert.Equal("booking_balance_balance-1_1", begun.IdempotencyKey);
    }

    [Fact]
    public async Task ChargeAsync_AStaleCopyOfAPaidBalance_IsNotChargedAgain()
    {
        // Arrange
        var stale = Copy();

        _stored.Status = BookingBalanceStatus.Paid;

        // Act
        await CreateCharger().ChargeAsync(stale, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_begun);
    }

    private BookingBalance Copy()
        => new()
        {
            ItemId = _stored.ItemId,
            OrderId = _stored.OrderId,
            OrderNumber = _stored.OrderNumber,
            TransactionId = _stored.TransactionId,
            DepositSessionId = _stored.DepositSessionId,
            Amount = _stored.Amount,
            Currency = _stored.Currency,
            Status = _stored.Status,
            Attempts = _stored.Attempts,
        };

    private BookingBalanceCharger CreateCharger()
    {
        var balances = new Mock<IBookingBalanceStore>();

        balances.Setup(store => store.FindByIdAsync(_stored.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(() => _stored);
        balances.Setup(store => store.UpdateAsync(It.IsAny<BookingBalance>(), It.IsAny<CancellationToken>()))
            .Callback((BookingBalance balance, CancellationToken _) =>
            {
                _stored.Status = balance.Status;
                _stored.Attempts = balance.Attempts;
            })
            .Returns(ValueTask.CompletedTask);

        var transactions = new Mock<ITransactionManager>();

        transactions.Setup(manager => manager.FindByIdAsync("transaction-1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new Transaction
            {
                ItemId = "transaction-1",
                Currency = "USD",
                TotalAmount = 80m,
                Status = TransactionStatus.Outstanding,
            }));

        var attempts = new Mock<IPaymentAttemptStore>();

        attempts.Setup(store => store.GetBySessionAsync("deposit-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PaymentAttempt { ItemId = "deposit-attempt", ProviderKey = "card", State = PaymentAttemptState.Succeeded }]);

        var savedCards = new Mock<ICheckoutSavedPaymentMethodProvider>();

        savedCards.Setup(provider => provider.Key).Returns("card");
        savedCards.Setup(provider => provider.GetSavedPaymentMethodAsync(It.IsAny<PaymentAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavedPaymentMethod { ProviderKey = "card", CustomerReference = "cus_1", PaymentMethodReference = "pm_1" });

        var services = new ServiceCollection()
            .AddSingleton(balances.Object)
            .AddSingleton(Mock.Of<IBookingStore>())
            .AddSingleton(transactions.Object)
            .AddSingleton(_engine.Object)
            .AddSingleton(Mock.Of<ICheckoutSessionStore>())
            .AddSingleton(attempts.Object)
            .AddSingleton<IEnumerable<ICheckoutSavedPaymentMethodProvider>>([savedCards.Object])
            .AddSingleton<IEnumerable<IBookingPaymentHandler>>([])
            .AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance))
            .AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc)))
            .AddSingleton<ILogger<BookingBalanceCharger>>(NullLogger<BookingBalanceCharger>.Instance)
            .AddSingleton<IStringLocalizer<BookingBalanceCharger>>(new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<BookingBalanceCharger>())
            .BuildServiceProvider();

        return ActivatorUtilities.CreateInstance<BookingBalanceCharger>(services);
    }
}
