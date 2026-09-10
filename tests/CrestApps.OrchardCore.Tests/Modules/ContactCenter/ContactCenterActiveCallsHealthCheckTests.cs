using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterActiveCallsHealthCheckTests
{
    private static readonly DateTime _now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CountActiveAsync_CountsOnlyCallsWithoutAnEndTime()
    {
        // Arrange
        var databasePath = DatabasePath("active-calls-count");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveCallAsync(seedSession, "live-1", endedUtc: null);
                await SaveCallAsync(seedSession, "live-2", endedUtc: null);
                await SaveCallAsync(seedSession, "ended-1", endedUtc: _now.AddMinutes(-5));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var assertSession = store.CreateSession();
            var callSessionStore = new CallSessionStore(assertSession);

            var active = await callSessionStore.CountActiveAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, active);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_ReportsActiveCallCountAsHealthyData()
    {
        // Arrange
        var databasePath = DatabasePath("active-calls-health");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveCallAsync(seedSession, "live-1", endedUtc: null);
                await SaveCallAsync(seedSession, "ended-1", endedUtc: _now.AddMinutes(-1));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var assertSession = store.CreateSession();
            var check = new ContactCenterActiveCallsHealthCheck(new CallSessionStore(assertSession));

            // Act
            var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal(1, Assert.IsType<int>(result.Data["active_calls"]));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"contact-center-{prefix}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new CallSessionIndexProvider(new ProviderIdentityResolver([]))]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        var migration = new CallSessionIndexMigrations(store, new ProviderIdentityResolver([]))
        {
            SchemaBuilder = schemaBuilder,
        };

        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveCallAsync(ISession session, string itemId, DateTime? endedUtc)
    {
        var callSession = new CallSession
        {
            ItemId = itemId,
            InteractionId = $"interaction-{itemId}",
            ProviderName = "ProviderA",
            ProviderCallId = $"call-{itemId}",
            CreatedUtc = _now,
            EndedUtc = endedUtc,
        }.RestorePersistedState(endedUtc is null ? VoiceCallState.Connected : VoiceCallState.Ended);

        await new CallSessionStore(session).CreateAsync(callSession, TestContext.Current.CancellationToken);
    }
}
