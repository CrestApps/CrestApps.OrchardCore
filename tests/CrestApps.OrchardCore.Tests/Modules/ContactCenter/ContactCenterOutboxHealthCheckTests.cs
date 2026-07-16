using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterOutboxHealthCheckTests
{
    private static readonly DateTime _now = new(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CountMethods_ReturnStatusAndOverdueCounts()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-outbox-health-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveMessageAsync(seedSession, "m1", OutboxMessageStatus.Pending, _now.AddMinutes(-5));
                await SaveMessageAsync(seedSession, "m2", OutboxMessageStatus.Claimed, _now.AddMinutes(-1));
                await SaveMessageAsync(seedSession, "m3", OutboxMessageStatus.Pending, _now.AddMinutes(10));
                await SaveMessageAsync(seedSession, "m4", OutboxMessageStatus.DeadLettered, _now);
                await SaveMessageAsync(seedSession, "m5", OutboxMessageStatus.DeadLettered, _now);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var assertSession = store.CreateSession();
            var outboxStore = new ContactCenterOutboxStore(assertSession);

            var deadLettered = await outboxStore.CountByStatusAsync(OutboxMessageStatus.DeadLettered, TestContext.Current.CancellationToken);
            var overdue = await outboxStore.CountOverdueAsync(_now, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, deadLettered);
            Assert.Equal(2, overdue);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDeadLettersPresent_ReportsDegraded()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-outbox-health-degraded-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveMessageAsync(seedSession, "m1", OutboxMessageStatus.DeadLettered, _now);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var assertSession = store.CreateSession();
            var check = CreateCheck(assertSession);

            // Act
            var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HealthStatus.Degraded, result.Status);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_WhenQueueClean_ReportsHealthy()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-outbox-health-healthy-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveMessageAsync(seedSession, "m1", OutboxMessageStatus.Pending, _now.AddMinutes(30));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var assertSession = store.CreateSession();
            var check = CreateCheck(assertSession);

            // Act
            var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static ContactCenterOutboxHealthCheck CreateCheck(ISession session)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);

        return new ContactCenterOutboxHealthCheck(
            new ContactCenterOutboxStore(session),
            Options.Create(new ContactCenterHealthCheckOptions()),
            clock.Object);
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new ContactCenterOutboxMessageIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EventId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveMessageAsync(
        ISession session,
        string itemId,
        OutboxMessageStatus status,
        DateTime nextAttemptUtc)
    {
        await session.SaveAsync(
            new ContactCenterOutboxMessage
            {
                ItemId = itemId,
                EventId = $"event-{itemId}",
                EventType = "InteractionCreated",
                Status = status,
                NextAttemptUtc = nextAttemptUtc,
                CreatedUtc = _now,
                ModifiedUtc = _now,
            },
            collection: ContactCenterConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
