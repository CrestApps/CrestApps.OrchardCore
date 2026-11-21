using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Handlers;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// L-checkout-decline-tracked-copy (final review): the decline handler read the attempt, then took the checkout's lock and
/// read it again, but through the same YesSql session, whose identity map handed back the copy from before the lock. An
/// edit that superseded the attempt in between was overwritten with "failed", and the next completion check failed the
/// checkout. Two sessions over a real SQLite store, the edit committed while the handler waits for the lock.
/// </summary>
public sealed class CardDeclineConcurrencySqliteTests : IAsyncLifetime
{
    private const string CardProvider = "card";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-declines-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new PaymentAttemptIndexProvider()]);
        await _store.InitializeAsync(cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            await new CheckoutMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await new PaymentAttemptStore(session, new TestClock(DateTime.UtcNow)).CreateAsync(new PaymentAttempt
            {
                ItemId = "attempt-1",
                SessionId = "session-1",
                ProviderKey = CardProvider,
                ObligationId = CheckoutObligations.OneTime,
                ProviderReference = "pi_1",
                ExpectedAmount = 30m,
                Currency = "USD",
                State = PaymentAttemptState.Pending,
            }, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AnEditThatSupersedesTheAttemptWhileTheHandlerWaits_IsNotOverwrittenWithFailed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // The edit: another request supersedes the attempt and commits, just before the handler gets the checkout's lock.
        var distributedLock = new InterceptingLock("CHECKOUT_SESSION_", async () =>
        {
            await using var edit = _store.CreateSession();
            var store = new PaymentAttemptStore(edit, new TestClock(DateTime.UtcNow));
            var attempt = await store.FindByIdAsync("attempt-1", cancellationToken);

            attempt.State = PaymentAttemptState.Canceled;
            attempt.IsSuperseded = true;

            await store.UpdateAsync(attempt, cancellationToken);
            await edit.SaveChangesAsync(cancellationToken);
        });

        await using (var request = _store.CreateSession())
        {
            var handler = CreateHandler(request, distributedLock);
            var failed = new PaymentFailedContext { TransactionId = "pi_1", GatewayId = CardProvider, EventId = "evt_1" };
            failed.Data[CardDeclinePaymentEventHandler.AttemptIdDataKey] = "attempt-1";

            await handler.PaymentFailedAsync(failed, cancellationToken);
            await request.SaveChangesAsync(cancellationToken);
        }

        await using var check = _store.CreateSession();
        var saved = await new PaymentAttemptStore(check, new TestClock(DateTime.UtcNow)).FindByIdAsync("attempt-1", cancellationToken);

        Assert.True(saved.IsSuperseded);
        Assert.Equal(PaymentAttemptState.Canceled, saved.State);
    }

    private static CardDeclinePaymentEventHandler CreateHandler(ISession session, IDistributedLock distributedLock)
    {
        // One decline is enough to cancel, so the handler goes for the lock at the first.
        var options = new AttemptLimitOptions();
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineAttempt, 1, TimeSpan.FromHours(24), TimeSpan.FromHours(24));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentDeclineEvent, 1, TimeSpan.FromDays(3));

        var clock = new TestClock(DateTime.UtcNow);
        var limiter = new DistributedAttemptLimiter(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new LocalLock(NullLogger<LocalLock>.Instance),
            new ShellSettings { Name = "Default" },
            clock,
            Options.Create(options),
            NullLogger<DistributedAttemptLimiter>.Instance);

        var provider = new Mock<ICheckoutPaymentProvider>();
        provider.SetupGet(value => value.Key).Returns(CardProvider);
        provider.SetupGet(value => value.Capabilities).Returns(new PaymentProviderCapabilities { SupportsOneTimePayments = true, IsCardNotPresent = true });
        provider.Setup(value => value.CancelAsync(It.IsAny<CancelPaymentContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(PaymentCancelResult.Success());

        var resolver = new Mock<ICheckoutPaymentProviderResolver>();
        resolver.Setup(value => value.GetProvider(CardProvider)).Returns(provider.Object);

        var sessions = new InMemoryCheckoutSessionStore();
        sessions.Seed(new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.AwaitingProvider, Currency = "USD" });

        var services = new ServiceCollection()
            .AddSingleton<IAttemptLimiter>(limiter)
            .AddSingleton(resolver.Object)
            .AddSingleton(Mock.Of<IPaymentDeclineRecorder>())
            .AddSingleton<IPaymentAttemptStore>(new PaymentAttemptStore(session, clock))
            .AddSingleton<ICheckoutSessionStore>(sessions)
            .AddSingleton(distributedLock)
            .AddSingleton(session)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();

        return ActivatorUtilities.CreateInstance<CardDeclinePaymentEventHandler>(services);
    }

    // Runs an action the first time a lock whose key starts with a prefix is asked for, then hands out locks as usual.
    private sealed class InterceptingLock : IDistributedLock
    {
        private readonly LocalLock _inner = new(NullLogger<LocalLock>.Instance);
        private readonly string _prefix;
        private Func<Task> _before;

        public InterceptingLock(string prefix, Func<Task> before)
        {
            _prefix = prefix;
            _before = before;
        }

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => _inner.AcquireLockAsync(key, expiration);

        public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            if (key.StartsWith(_prefix, StringComparison.Ordinal) && Interlocked.Exchange(ref _before, null) is { } before)
            {
                await before();
            }

            return await _inner.TryAcquireLockAsync(key, timeout, expiration);
        }

        public Task<bool> IsLockAcquiredAsync(string key)
            => _inner.IsLockAcquiredAsync(key);
    }
}
