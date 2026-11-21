using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
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
/// click while the background task runs), and a copy of a balance read before it was paid is never charged again, nor
/// recorded as failed.
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

    private readonly Mock<ISavedPaymentMethodChargeService> _charges = new();
    private int _charged;
    private Func<Task> _gate = () => Task.CompletedTask;

    public BookingBalanceChargeConcurrencyTests()
    {
        _charges.Setup(service => service.ChargeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken _) =>
            {
                Interlocked.Increment(ref _charged);

                await _gate();

                return new SavedPaymentMethodChargeResult { ChargeStatus = SavedPaymentMethodChargeStatus.Succeeded };
            });
    }

    [Fact]
    public async Task ChargeAsync_TwoAtOnce_ChargeOnce()
    {
        // Arrange
        var charger = CreateCharger();
        var started = new TaskCompletionSource();
        var finish = new TaskCompletionSource();

        _gate = async () =>
        {
            started.TrySetResult();
            await finish.Task;
        };

        // Act
        var first = charger.ChargeAsync(Copy(), TestContext.Current.CancellationToken);

        await started.Task;
        var second = await charger.ChargeAsync(Copy(), TestContext.Current.CancellationToken);

        finish.SetResult();
        await first;

        // Assert
        Assert.Equal(1, _charged);
        Assert.NotNull(second);
        Assert.Equal(BookingBalanceStatus.Paid, _stored.Status);
    }

    [Fact]
    public async Task ChargeAsync_AStaleCopyOfAPaidBalance_IsNotChargedAgainNorFailed()
    {
        // Arrange
        var stale = Copy();

        _stored.Status = BookingBalanceStatus.Paid;

        // Act
        var failure = await CreateCharger().ChargeAsync(stale, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(failure);
        Assert.Equal(0, _charged);
        Assert.Equal(BookingBalanceStatus.Paid, _stored.Status);
    }

    [Fact]
    public async Task ChargeNow_OnABalanceThatFailedForGood_ChargesItAgain()
    {
        // Arrange: the stored balance failed for good; "Charge now" starts the copy it loaded over, as
        // BookingsAdminController.ChargeBalance does.
        _stored.Status = BookingBalanceStatus.Failed;
        _stored.Attempts = 3;

        var restarted = Copy();

        restarted.Status = BookingBalanceStatus.Scheduled;
        restarted.Attempts = 0;

        // Act
        var failure = await CreateCharger().ChargeAsync(restarted, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(failure);
        Assert.Equal(1, _charged);
        Assert.Equal(BookingBalanceStatus.Paid, _stored.Status);
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
                AutoCharge = new TransactionAutoCharge { SourceSessionId = "deposit-1" },
            }));

        var services = new ServiceCollection()
            .AddSingleton(balances.Object)
            .AddSingleton(Mock.Of<IBookingStore>(store => store.ListByOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult<IReadOnlyList<Booking>>(Array.Empty<Booking>())))
            .AddSingleton(transactions.Object)
            .AddSingleton(_charges.Object)
            .AddSingleton<IEnumerable<IBookingPaymentHandler>>([])
            .AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance))
            .AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc)))
            .AddSingleton<ILogger<BookingBalanceCharger>>(NullLogger<BookingBalanceCharger>.Instance)
            .AddSingleton<IStringLocalizer<BookingBalanceCharger>>(new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<BookingBalanceCharger>())
            .BuildServiceProvider();

        return ActivatorUtilities.CreateInstance<BookingBalanceCharger>(services);
    }
}
