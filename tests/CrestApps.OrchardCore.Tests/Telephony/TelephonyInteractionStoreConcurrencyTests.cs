using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Migrations;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Verifies that concurrent telephony interaction writes cannot silently discard each other's work.
/// </summary>
public sealed class TelephonyInteractionStoreConcurrencyTests
{
    private static readonly DateTime _startedUtc = new(2026, 8, 3, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpdateAsync_WhenAnotherWriterCommittedFirst_FailsInsteadOfOverwritingTheWinner()
    {
        // Arrange
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"crestapps-telephony-interaction-cas-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store);

            await using var sessionA = store.CreateSession();
            await using var sessionB = store.CreateSession();
            var storeA = new DefaultTelephonyInteractionStore(sessionA);
            var storeB = new DefaultTelephonyInteractionStore(sessionB);

            var fromA = await storeA.FindByCallIdAsync(
                "user-1",
                "call-1",
                TestContext.Current.CancellationToken);
            var fromB = await storeB.FindByCallIdAsync(
                "user-1",
                "call-1",
                TestContext.Current.CancellationToken);

            // Act
            fromA.Outcome = CallOutcome.Completed;
            fromA.EndedUtc = _startedUtc.AddMinutes(2);
            await storeA.UpdateAsync(fromA, TestContext.Current.CancellationToken);
            await sessionA.SaveChangesAsync(TestContext.Current.CancellationToken);

            fromB.Outcome = CallOutcome.Missed;
            await storeB.UpdateAsync(fromB, TestContext.Current.CancellationToken);
            var exception = await Record.ExceptionAsync(
                () => sessionB.SaveChangesAsync(TestContext.Current.CancellationToken));

            // Assert
            Assert.IsType<ConcurrencyException>(exception);

            var persisted = await ReadAsync(store);
            Assert.Equal(CallOutcome.Completed, persisted.Outcome);
            Assert.Equal(_startedUtc.AddMinutes(2), persisted.EndedUtc);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(
            configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        store.RegisterIndexes([new TelephonyInteractionIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new TelephonyInteractionMigrations
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        };

        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SeedAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var interactionStore = new DefaultTelephonyInteractionStore(session);
        var interaction = new TelephonyInteraction
        {
            InteractionId = "interaction-1",
            CallId = "call-1",
            ProviderName = "ProviderA",
            UserId = "user-1",
            UserName = "agent",
            Direction = CallDirection.Inbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _startedUtc,
        };

        await interactionStore.CreateAsync(interaction, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<TelephonyInteraction> ReadAsync(IStore store)
    {
        await using var session = store.CreateSession();

        return await session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.InteractionId == "interaction-1")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }
}
