using CrestApps.OrchardCore.Checkout.Indexes;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// The kept payment methods are found by owner and by the provider's reference, so they run through a real YesSql store:
/// a card kept twice must stay one record, and one owner must never see or remove another's card.
/// </summary>
public sealed class SavedPaymentMethodStoreSqliteTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SaveAsync_TheFirstCard_BecomesTheDefault_AndTheSecondDoesNot()
    {
        await RunAsync(async (store, session) =>
        {
            // Act
            var first = await store.SaveAsync("user-1", CreateMethod("pm_1"), "session-1", TestContext.Current.CancellationToken);
            var second = await store.SaveAsync("user-1", CreateMethod("pm_2"), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(first.IsDefault);
            Assert.False(second.IsDefault);
            Assert.Equal("session-1", first.SourceSessionId);

            var listed = await store.ListAsync("user-1", TestContext.Current.CancellationToken);

            Assert.Equal(["pm_1", "pm_2"], listed.Select(method => method.PaymentMethod.PaymentMethodReference));
        });
    }

    [Fact]
    public async Task SaveAsync_TheSameCardAgain_UpdatesItInsteadOfAddingASecond()
    {
        await RunAsync(async (store, session) =>
        {
            // Arrange
            var first = await store.SaveAsync("user-1", CreateMethod("pm_1", expirationYear: 2027), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            var again = await store.SaveAsync("user-1", CreateMethod("pm_1", expirationYear: 2031), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(first.ItemId, again.ItemId);

            var only = Assert.Single(await store.ListAsync("user-1", TestContext.Current.CancellationToken));

            Assert.Equal(2031, only.PaymentMethod.ExpirationYear);
        });
    }

    [Fact]
    public async Task Lookups_NeverReachAnotherOwnersCard()
    {
        await RunAsync(async (store, session) =>
        {
            // Arrange
            var theirs = await store.SaveAsync("user-2", CreateMethod("pm_theirs"), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            var found = await store.FindAsync("user-1", theirs.ItemId, TestContext.Current.CancellationToken);
            var removed = await store.RemoveAsync("user-1", theirs.ItemId, TestContext.Current.CancellationToken);
            var madeDefault = await store.SetDefaultAsync("user-1", theirs.ItemId, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(found);
            Assert.False(removed);
            Assert.False(madeDefault);
            Assert.Empty(await store.ListAsync("user-1", TestContext.Current.CancellationToken));
            Assert.Single(await store.ListAsync("user-2", TestContext.Current.CancellationToken));
            Assert.NotNull(await store.FindAsync("user-2", theirs.ItemId, TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task SetDefaultAsync_MovesTheDefaultToTheChosenCard()
    {
        await RunAsync(async (store, session) =>
        {
            // Arrange
            await store.SaveAsync("user-1", CreateMethod("pm_1"), cancellationToken: TestContext.Current.CancellationToken);
            var second = await store.SaveAsync("user-1", CreateMethod("pm_2"), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            var changed = await store.SetDefaultAsync("user-1", second.ItemId, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(changed);

            var listed = await store.ListAsync("user-1", TestContext.Current.CancellationToken);

            Assert.Equal(second.ItemId, listed[0].ItemId);
            Assert.Single(listed, method => method.IsDefault);
            Assert.Equal(second.ItemId, (await store.GetDefaultAsync("user-1", TestContext.Current.CancellationToken)).ItemId);
        });
    }

    [Fact]
    public async Task RemoveAsync_TheDefaultCard_MakesTheRemainingOneTheDefault()
    {
        await RunAsync(async (store, session) =>
        {
            // Arrange
            var first = await store.SaveAsync("user-1", CreateMethod("pm_1"), cancellationToken: TestContext.Current.CancellationToken);
            var second = await store.SaveAsync("user-1", CreateMethod("pm_2"), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            var removed = await store.RemoveAsync("user-1", first.ItemId, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(removed);

            var only = Assert.Single(await store.ListAsync("user-1", TestContext.Current.CancellationToken));

            Assert.Equal(second.ItemId, only.ItemId);
            Assert.True(only.IsDefault);
        });
    }

    [Fact]
    public async Task GetDefaultAsync_SkipsAnExpiredDefault()
    {
        await RunAsync(async (store, session) =>
        {
            // Arrange
            await store.SaveAsync("user-1", CreateMethod("pm_expired", expirationYear: 2025), cancellationToken: TestContext.Current.CancellationToken);
            var current = await store.SaveAsync("user-1", CreateMethod("pm_current", expirationYear: 2030), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            var chosen = await store.GetDefaultAsync("user-1", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(current.ItemId, chosen.ItemId);
        });
    }

    [Fact]
    public async Task RemoveAllAsync_ForgetsOnlyThatOwnersCards()
    {
        await RunAsync(async (store, session) =>
        {
            // Arrange
            await store.SaveAsync("user-1", CreateMethod("pm_1"), cancellationToken: TestContext.Current.CancellationToken);
            await store.SaveAsync("user-1", CreateMethod("pm_2"), cancellationToken: TestContext.Current.CancellationToken);
            await store.SaveAsync("user-2", CreateMethod("pm_3"), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            var removed = await store.RemoveAllAsync("user-1", TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, removed);
            Assert.Empty(await store.ListAsync("user-1", TestContext.Current.CancellationToken));
            Assert.Single(await store.ListAsync("user-2", TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task MarkChargedAsync_RecordsWhenTheCardWasLastCharged()
    {
        await RunAsync(async (store, session) =>
        {
            // Arrange
            var method = await store.SaveAsync("user-1", CreateMethod("pm_1"), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            await store.MarkChargedAsync(method.ItemId, _now.AddDays(3), TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(_now.AddDays(3), (await store.FindByIdAsync(method.ItemId, TestContext.Current.CancellationToken)).LastChargedUtc);
        });
    }

    private static SavedPaymentMethod CreateMethod(string reference, int expirationYear = 2030)
        => new()
        {
            ProviderKey = "test",
            CustomerReference = "cus_1",
            PaymentMethodReference = reference,
            Brand = "visa",
            Last4 = "4242",
            ExpirationMonth = 12,
            ExpirationYear = expirationYear,
        };

    private static async Task RunAsync(Func<SavedPaymentMethodStore, ISession, Task> test)
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-saved-cards-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(path);

        try
        {
            var clock = new TestClock(_now);

            await using var session = store.CreateSession();

            // Each card is kept a moment after the one before, so "newest first" has an order to follow.
            var paymentMethods = new SavedPaymentMethodStore(session, new SteppingClock(clock));

            await test(paymentMethods, session);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new CustomerPaymentMethodIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        var migration = new CheckoutMigrations { SchemaBuilder = schemaBuilder };
        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private sealed class SteppingClock : IClock
    {
        private readonly TestClock _clock;
        private int _ticks;

        public SteppingClock(TestClock clock)
            => _clock = clock;

        public DateTime UtcNow
            => _clock.UtcNow.AddSeconds(_ticks++);

        public ITimeZone GetTimeZone(string timeZoneId)
            => _clock.GetTimeZone(timeZoneId);

        public ITimeZone[] GetTimeZones()
            => _clock.GetTimeZones();

        public ITimeZone GetSystemTimeZone()
            => _clock.GetSystemTimeZone();

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone)
            => _clock.ConvertToTimeZone(dateTimeOffset, timeZone);
    }
}