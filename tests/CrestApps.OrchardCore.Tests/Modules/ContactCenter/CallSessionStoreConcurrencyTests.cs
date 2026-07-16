using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class CallSessionStoreConcurrencyTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Update_AfterHigherSequenceCommitted_StaleLowerSequenceFailsAndDoesNotLowerHighWater()
    {
        var databasePath = DatabasePath("callsession-highwater-cas");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedCallSessionAsync(store, sequence: null, ContactCenterCallState.Ringing);

            await using var sessionA = store.CreateSession();
            await using var sessionB = store.CreateSession();
            var storeA = new CallSessionStore(sessionA);
            var storeB = new CallSessionStore(sessionB);

            // Both workers read the same committed version before either writes.
            var callFromA = await storeA.FindByIdAsync("session-1", TestContext.Current.CancellationToken);
            var callFromB = await storeB.FindByIdAsync("session-1", TestContext.Current.CancellationToken);

            // Act
            // Worker A applies sequence N+1 and commits first.
            callFromA.HighWaterSequence = 6;
            callFromA.State = ContactCenterCallState.Connected;
            await storeA.UpdateAsync(callFromA, TestContext.Current.CancellationToken);
            await sessionA.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Worker B loaded the pre-A version and now tries to commit the lower sequence N.
            callFromB.HighWaterSequence = 5;
            callFromB.State = ContactCenterCallState.Ringing;
            await storeB.UpdateAsync(callFromB, TestContext.Current.CancellationToken);
            var exception = await Record.ExceptionAsync(() => sessionB.SaveChangesAsync(TestContext.Current.CancellationToken));

            // Assert
            Assert.True(
                exception is ConcurrencyException or DbException,
                $"Expected an optimistic-concurrency failure but received {exception?.GetType().Name ?? "no exception"}.");

            var persisted = await ReadCallSessionAsync(store, "session-1");
            Assert.Equal(6, persisted.HighWaterSequence);
            Assert.Equal(ContactCenterCallState.Connected, persisted.State);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task Update_TwoWorkersReadSameVersion_OnlyOneCommits()
    {
        var databasePath = DatabasePath("callsession-cas-exclusive");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedCallSessionAsync(store, sequence: 1, ContactCenterCallState.Ringing);

            await using var sessionA = store.CreateSession();
            await using var sessionB = store.CreateSession();
            var storeA = new CallSessionStore(sessionA);
            var storeB = new CallSessionStore(sessionB);
            var callFromA = await storeA.FindByIdAsync("session-1", TestContext.Current.CancellationToken);
            var callFromB = await storeB.FindByIdAsync("session-1", TestContext.Current.CancellationToken);

            callFromA.HighWaterSequence = 3;
            callFromA.State = ContactCenterCallState.Connected;
            callFromB.HighWaterSequence = 2;
            callFromB.State = ContactCenterCallState.OnHold;
            await storeA.UpdateAsync(callFromA, TestContext.Current.CancellationToken);
            await storeB.UpdateAsync(callFromB, TestContext.Current.CancellationToken);

            // Act
            var attempts = await Task.WhenAll(
                CaptureAsync(sessionA),
                CaptureAsync(sessionB));

            // Assert
            Assert.Single(attempts, exception => exception is null);
            var failure = Assert.Single(attempts, exception => exception is not null);
            Assert.True(
                failure is ConcurrencyException or DbException,
                $"Expected an optimistic-concurrency failure but received {failure.GetType().Name}.");

            // Whichever worker won, the persisted high-water advanced and was never reversed below the seed.
            var persisted = await ReadCallSessionAsync(store, "session-1");
            Assert.True(persisted.HighWaterSequence >= 2);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static async Task<Exception> CaptureAsync(ISession session)
    {
        try
        {
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"contact-center-{prefix}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new CallSessionIndexProvider(new ProviderIdentityResolver([]))]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);
        await CreateIndexSchemaAsync(store);

        return store;
    }

    private static async Task CreateIndexSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        var migration = new CallSessionIndexMigrations(store, new ProviderIdentityResolver([]))
        {
            SchemaBuilder = schemaBuilder,
        };
        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SeedCallSessionAsync(IStore store, long? sequence, ContactCenterCallState state)
    {
        await using var session = store.CreateSession();
        var callSessionStore = new CallSessionStore(session);
        var callSession = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            ProviderName = "ProviderA",
            ProviderCallId = "call-1",
            State = state,
            HighWaterSequence = sequence,
            CreatedUtc = _now,
        };
        await callSessionStore.CreateAsync(callSession, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<CallSession> ReadCallSessionAsync(IStore store, string itemId)
    {
        await using var session = store.CreateSession();

        return await new CallSessionStore(session).FindByIdAsync(itemId, TestContext.Current.CancellationToken);
    }
}
