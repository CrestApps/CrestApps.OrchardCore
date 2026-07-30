using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Exercises the appended-contribution metric path against a real database. Counting by reading a daily total,
/// adding one and writing it back makes that single row a serialization point for every event of the same type
/// on the same day, and under the store's optimistic concurrency the losing writer either fails its whole
/// request or overwrites a count it never read. Mocks cannot show either outcome, because both are produced by
/// the database rather than by the code: only concurrent sessions committing against real storage can.
/// </summary>
public sealed class ContactCenterMetricRollupPersistenceTests
{
    private static readonly DateTime _now = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecordAsync_WhenManyWritersCountTheSameEventOnTheSameDay_KeepsEveryCount()
    {
        // Arrange
        var databasePath = DatabasePath("concurrent");
        var store = await CreateStoreAsync(databasePath);
        const int writers = 24;

        try
        {
            // Act
            // Each writer commits in its own session, exactly as an isolated handler scope does. Every commit
            // has to succeed: a failure here is the contention this design exists to remove, and a success
            // count below the number of writers is a lost update.
            var recorded = new int[writers];

            await Parallel.ForAsync(0, writers, TestContext.Current.CancellationToken, async (index, cancellationToken) =>
            {
                await using var session = store.CreateSession();
                var metrics = CreateMetricsService(session);

                await metrics.RecordAsync("OfferAccepted", _now, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);

                recorded[index] = 1;
            });

            // Assert
            Assert.Equal(writers, recorded.Sum());

            await using (var beforeSession = store.CreateSession())
            {
                var pending = await new ContactCenterMetricDeltaStore(beforeSession)
                    .ListByDateRangeAsync(_now.Date, _now.Date, TestContext.Current.CancellationToken);

                // Every writer appended its own row rather than meeting the others on one.
                Assert.Equal(writers, pending.Count);

                // Nothing wrote the daily total, so there was no row for the writers to contend for.
                var total = await new ContactCenterMetricStore(beforeSession).FindAsync(
                    "2026-07-15",
                    "OfferAccepted",
                    TestContext.Current.CancellationToken);

                Assert.Null(total);

                // A reader still reports the full count before anything has been folded.
                var summary = await CreateMetricsService(beforeSession).GetSummaryAsync(
                    DateOnly.FromDateTime(_now),
                    DateOnly.FromDateTime(_now),
                    TestContext.Current.CancellationToken);

                Assert.Equal(writers, summary["OfferAccepted"]);
            }

            await using (var rollupSession = store.CreateSession())
            {
                var folded = await CreateRollupService(rollupSession).RollupAsync(TestContext.Current.CancellationToken);

                Assert.Equal(writers, folded);
            }

            await using (var afterSession = store.CreateSession())
            {
                var total = await new ContactCenterMetricStore(afterSession).FindAsync(
                    "2026-07-15",
                    "OfferAccepted",
                    TestContext.Current.CancellationToken);

                Assert.NotNull(total);
                Assert.Equal(writers, total.Count);

                // The fold moved the count, it did not duplicate it: a reader that added the folded total to
                // contributions the roller had already removed would report twice the traffic.
                var summary = await CreateMetricsService(afterSession).GetSummaryAsync(
                    DateOnly.FromDateTime(_now),
                    DateOnly.FromDateTime(_now),
                    TestContext.Current.CancellationToken);

                Assert.Equal(writers, summary["OfferAccepted"]);
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RollupAsync_RemovesOnlyTheContributionsItRead_SoNothingAppendedDuringTheFoldIsLost()
    {
        // Arrange
        // A contribution appended between the roller's read and its delete is the case that decides whether the
        // fold may delete by predicate. Nothing about the fold is slow enough to hit that window by chance, so
        // the window is opened deliberately: the store handed to the roller commits one more contribution, from
        // its own session, the moment the batch read returns.
        var databasePath = DatabasePath("interleaved");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                var metrics = CreateMetricsService(seedSession);

                for (var i = 0; i < 3; i++)
                {
                    await metrics.RecordAsync("OfferAccepted", _now, TestContext.Current.CancellationToken);
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            var interleaved = 0;

            await using (var rollupSession = store.CreateSession())
            {
                var interleaving = new InterleavingMetricDeltaStore(
                    new ContactCenterMetricDeltaStore(rollupSession),
                    () => AppendFromAnotherSessionAsync(store));

                var rollup = new ContactCenterMetricRollupService(
                    interleaving,
                    new ContactCenterMetricStore(rollupSession),
                    rollupSession,
                    CreateClock());

                var folded = await rollup.RollupAsync(TestContext.Current.CancellationToken);

                // The three it read, and only those. The interleaved contribution arrived after the read, so it
                // belongs to the next fold.
                Assert.Equal(3, folded);
                Assert.True(interleaving.Interleaved > 0, "The interleaved append never ran, so the window this test exists to cover was never opened.");

                interleaved = interleaving.Interleaved;
            }

            // Assert
            await using (var verificationSession = store.CreateSession())
            {
                var total = await new ContactCenterMetricStore(verificationSession).FindAsync(
                    "2026-07-15",
                    "OfferAccepted",
                    TestContext.Current.CancellationToken);

                Assert.NotNull(total);
                Assert.Equal(3, total.Count);

                // The interleaved contribution survived the fold that never read it. Deleting by predicate
                // instead of by read row would have removed it here, and its event would have been counted by
                // nobody.
                var pending = await new ContactCenterMetricDeltaStore(verificationSession)
                    .ListByDateRangeAsync(_now.Date, _now.Date, TestContext.Current.CancellationToken);

                Assert.Equal(interleaved, pending.Count);

                var summary = await CreateMetricsService(verificationSession).GetSummaryAsync(
                    DateOnly.FromDateTime(_now),
                    DateOnly.FromDateTime(_now),
                    TestContext.Current.CancellationToken);

                Assert.Equal(3 + interleaved, summary["OfferAccepted"]);
            }

            await using (var secondRollupSession = store.CreateSession())
            {
                var folded = await CreateRollupService(secondRollupSession).RollupAsync(TestContext.Current.CancellationToken);

                Assert.True(folded > 0);
            }

            await using (var finalSession = store.CreateSession())
            {
                var total = await new ContactCenterMetricStore(finalSession).FindAsync(
                    "2026-07-15",
                    "OfferAccepted",
                    TestContext.Current.CancellationToken);

                Assert.NotNull(total);
                Assert.Equal(3 + 1, total.Count);

                var pending = await new ContactCenterMetricDeltaStore(finalSession)
                    .ListByDateRangeAsync(_now.Date, _now.Date, TestContext.Current.CancellationToken);

                Assert.Empty(pending);
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RollupAsync_FoldsABacklogLargerThanOneBatch_WithoutOneReadPerContribution()
    {
        // Arrange
        // The fold is batched, so a backlog larger than a batch has to be drained by repeated reads. A read per
        // contribution would produce the same totals, so only counting the statements distinguishes a batched
        // drain from a loop over single rows.
        var databasePath = DatabasePath("backlog");
        var connectionFactory = new RecordingConnectionFactory($"Data Source={databasePath};Pooling=False");
        var store = await CreateStoreAsync(databasePath, connectionFactory);
        const int backlog = 1200;

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                var metrics = CreateMetricsService(seedSession);

                for (var i = 0; i < backlog; i++)
                {
                    await metrics.RecordAsync(i % 2 == 0 ? "OfferAccepted" : "OfferRejected", _now, TestContext.Current.CancellationToken);
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var deltaTable = store.Configuration.TableNameConvention.GetIndexTable(
                typeof(ContactCenterEventMetricDeltaIndex),
                ContactCenterConstants.CollectionName);

            connectionFactory.Clear();

            // Act
            await using (var rollupSession = store.CreateSession())
            {
                var folded = await CreateRollupService(rollupSession).RollupAsync(TestContext.Current.CancellationToken);

                Assert.Equal(backlog, folded);
            }

            // Assert
            var reads = connectionFactory.Statements.Count(statement =>
                statement.Contains(deltaTable, StringComparison.OrdinalIgnoreCase)
                && statement.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase));

            // Three full batches and the read that finds the table empty. The upper bound is what matters: a
            // read per contribution would be twelve hundred.
            Assert.InRange(reads, 1, 8);

            await using var verificationSession = store.CreateSession();
            var accepted = await new ContactCenterMetricStore(verificationSession).FindAsync(
                "2026-07-15",
                "OfferAccepted",
                TestContext.Current.CancellationToken);
            var rejected = await new ContactCenterMetricStore(verificationSession).FindAsync(
                "2026-07-15",
                "OfferRejected",
                TestContext.Current.CancellationToken);

            Assert.NotNull(accepted);
            Assert.NotNull(rejected);
            Assert.Equal(backlog / 2, accepted.Count);
            Assert.Equal(backlog / 2, rejected.Count);

            var pending = await new ContactCenterMetricDeltaStore(verificationSession)
                .ListByDateRangeAsync(_now.Date, _now.Date, TestContext.Current.CancellationToken);

            Assert.Empty(pending);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RollupAsync_AddsToTheTotalAlreadyThere_RatherThanReplacingIt()
    {
        // Arrange
        // The second fold of a day has to find the total the first fold left and add to it. Replacing it would
        // pass every single-fold test while quietly discarding every earlier hour of the day.
        var databasePath = DatabasePath("accumulate");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await RecordAndFoldAsync(store, 2);
            await RecordAndFoldAsync(store, 5);

            // Assert
            await using var verificationSession = store.CreateSession();
            var total = await new ContactCenterMetricStore(verificationSession).FindAsync(
                "2026-07-15",
                "OfferAccepted",
                TestContext.Current.CancellationToken);

            Assert.NotNull(total);
            Assert.Equal(7, total.Count);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-date")]
    [InlineData("2026-13-01")]
    [InlineData("07/15/2026")]
    public void MetricDateKey_RefusesAKeyItCannotParse_RatherThanReturningAnUncountableDate(string dateKey)
    {
        // The roller stamps the daily total's date by parsing the key the contributions were grouped under.
        // Returning a sentinel for a key that cannot be parsed would write a total dated to the beginning of
        // time: the count would be persisted, no error would be raised, and it would never appear in any range
        // a caller asks for. Failing here keeps a corrupt key a visible fault instead of a silent loss.

        // Act & Assert
        Assert.Throws<FormatException>(() => ContactCenterMetricDateKey.Parse(dateKey));
    }

    [Fact]
    public void MetricDateKey_RoundTripsThroughTheFormatTheContributionsAreGroupedBy()
    {
        // Arrange
        var occurredUtc = new DateTime(2026, 7, 15, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var parsed = ContactCenterMetricDateKey.Parse(ContactCenterMetricDateKey.From(occurredUtc));

        // Assert
        Assert.Equal(new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc), parsed);
        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
    }

    private static async Task RecordAndFoldAsync(IStore store, int count)
    {
        await using (var session = store.CreateSession())
        {
            var metrics = CreateMetricsService(session);

            for (var i = 0; i < count; i++)
            {
                await metrics.RecordAsync("OfferAccepted", _now, TestContext.Current.CancellationToken);
            }

            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var rollupSession = store.CreateSession();
        await CreateRollupService(rollupSession).RollupAsync(TestContext.Current.CancellationToken);
    }

    private static async Task AppendFromAnotherSessionAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var metrics = CreateMetricsService(session);

        await metrics.RecordAsync("OfferAccepted", _now, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static ContactCenterMetricsService CreateMetricsService(ISession session)
        => new(
            new ContactCenterMetricStore(session),
            new ContactCenterMetricDeltaStore(session),
            CreateClock());

    private static ContactCenterMetricRollupService CreateRollupService(ISession session)
        => new(
            new ContactCenterMetricDeltaStore(session),
            new ContactCenterMetricStore(session),
            session,
            CreateClock());

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static string DatabasePath(string suffix)
        => Path.Combine(Path.GetTempPath(), $"contact-center-metric-rollup-{suffix}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath, IConnectionFactory connectionFactory = null)
    {
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False");

            if (connectionFactory is not null)
            {
                configuration.ConnectionFactory = connectionFactory;
            }
        });

        store.RegisterIndexes(
        [
            new ContactCenterEventMetricIndexProvider(),
            new ContactCenterEventMetricDeltaIndexProvider(),
        ]);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        var metricMigration = new ContactCenterEventMetricIndexMigrations(store)
        {
            SchemaBuilder = schemaBuilder,
        };
        var deltaMigration = new ContactCenterEventMetricDeltaIndexMigrations
        {
            SchemaBuilder = schemaBuilder,
        };

        await metricMigration.CreateAsync();
        await deltaMigration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
