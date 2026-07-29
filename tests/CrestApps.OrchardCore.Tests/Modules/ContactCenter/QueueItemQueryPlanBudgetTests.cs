using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;
using YesSql.Provider.Sqlite;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Asserts a query-plan and round-trip budget for the agent workspace. Every signed-in agent polls the
/// workspace continuously, and the poll asks how many items are waiting in each queue the agent belongs to. If
/// that question is answered one queue at a time then a single poll costs a query per queue, and if each of
/// those queries reads the queue item table end to end then the cost also grows with everything the contact
/// center has ever enqueued. Both regressions return identical results, so no functional test can see them: a
/// plan and a statement count are the only evidence.
/// </summary>
public sealed class QueueItemQueryPlanBudgetTests
{
    [Fact]
    public async Task WaitingCountByQueue_SeeksAnIndexInsteadOfScanningTheQueueItemTable()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(AppContext.BaseDirectory, "QueryPlanData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"queue-plan-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        try
        {
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, cancellationToken);

            await using (var seedSession = store.CreateSession())
            {
                var seedTransaction = await seedSession.BeginTransactionAsync(cancellationToken);
                await QueueItemQueryPlanFixture.MigrateAsync(store, store.Configuration, seedTransaction);
                await QueueItemQueryPlanFixture.SeedAsync(store.Configuration, seedTransaction, cancellationToken);

                await using (var analyze = seedTransaction.Connection.CreateCommand())
                {
                    analyze.Transaction = seedTransaction;
                    analyze.CommandText = "ANALYZE;";
                    await analyze.ExecuteNonQueryAsync(cancellationToken);
                }

                await seedTransaction.CommitAsync(cancellationToken);
            }

            // Act
            var plan = await ExplainAsync(store, cancellationToken);

            // Assert
            // SQLite reports reading a table end to end as SCAN and an index seek as SEARCH. Asserting the
            // absence of a SCAN would prove nothing here: without the covering index the planner does not fall
            // back to a table scan, it seeks IDX_QueueItemIndex_Retention, which leads with Status and therefore
            // walks every waiting item in the tenant for each queue asked about. That plan is the regression, so
            // the budget is that no other index answers this question.
            var tableName = QueueItemQueryPlanFixture.TableName(store.Configuration);
            var rendered = string.Join(Environment.NewLine, plan);

            Assert.DoesNotContain(
                plan,
                line => line.Contains($"SCAN {tableName}", StringComparison.OrdinalIgnoreCase));

            Assert.DoesNotContain(
                plan,
                line => line.Contains("IDX_QueueItemIndex_Retention", StringComparison.OrdinalIgnoreCase));

            Assert.True(
                plan.Any(line => line.Contains("IDX_QueueItemIndex_WaitingByQueue", StringComparison.OrdinalIgnoreCase)),
                $"The workspace poll must seek IDX_QueueItemIndex_WaitingByQueue. Plan:{Environment.NewLine}{rendered}");

            // Both columns must be seek constraints, not just the leading one. Filtering the status per row
            // still seeks the index by queue, but it then has to walk every item the queue has ever held to
            // test each one, so the work still grows without bound while the plan still names the index.
            Assert.True(
                plan.Any(line => line.Contains("QueueId=?", StringComparison.OrdinalIgnoreCase)
                    && line.Contains("Status=?", StringComparison.OrdinalIgnoreCase)),
                $"The workspace poll must constrain both the queue and the status through the index rather than filtering the status per row. Plan:{Environment.NewLine}{rendered}");

            // A covering index answers the count from the index alone. Without it every matching row costs a
            // second read of the table to fetch columns the index does not carry.
            Assert.True(
                plan.Any(line => line.Contains("COVERING INDEX", StringComparison.OrdinalIgnoreCase)),
                $"The workspace poll must be answered from the index alone. Plan:{Environment.NewLine}{rendered}");
        }
        finally
        {
            store.Dispose();

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task WaitingCount_CostsTheSameNumberOfRoundTripsRegardlessOfHowManyQueuesTheAgentCovers()
    {
        // A plan budget alone cannot see an N+1: a per-queue count seeks the same index and produces the same
        // plan, it just runs once per queue. Counting the statements a single read issues is what distinguishes
        // one grouped query from a loop over well-planned ones.
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(AppContext.BaseDirectory, "QueryPlanData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"queue-roundtrip-{Guid.NewGuid():N}.db");
        var connectionFactory = new RecordingConnectionFactory($"Data Source={databasePath};Pooling=False");
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False");
            configuration.ConnectionFactory = connectionFactory;
        });

        try
        {
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, cancellationToken);

            await using (var seedSession = store.CreateSession())
            {
                var seedTransaction = await seedSession.BeginTransactionAsync(cancellationToken);
                await QueueItemQueryPlanFixture.MigrateAsync(store, store.Configuration, seedTransaction);
                await QueueItemQueryPlanFixture.SeedAsync(store.Configuration, seedTransaction, cancellationToken);
                await seedTransaction.CommitAsync(cancellationToken);
            }

            var tableName = QueueItemQueryPlanFixture.TableName(store.Configuration);

            // Act
            var oneQueue = await CountStatementsAsync(store, connectionFactory, tableName, 1, cancellationToken);
            var manyQueues = await CountStatementsAsync(store, connectionFactory, tableName, QueueItemQueryPlanFixture.SeededQueues, cancellationToken);

            // Assert
            // The single-queue case proves the recording sees the statement at all; without it the equality
            // below would hold just as well if nothing were recorded.
            Assert.Equal(1, oneQueue);
            Assert.Equal(oneQueue, manyQueues);
        }
        finally
        {
            store.Dispose();

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task WaitingCount_ReportsTheSameNumbersTheSingleQueueCountReports()
    {
        // The batch statement is hand written, so nothing in the type system ties it to the per-queue count it
        // replaced. Without this the statement could group, seek and return the wrong numbers while both the
        // plan budget and the round-trip budget stayed green. The items are written through the real store so
        // both counts read the same rows the product writes.
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(AppContext.BaseDirectory, "QueryPlanData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"queue-agreement-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        try
        {
            store.RegisterIndexes([new QueueItemIndexProvider()], ContactCenterConstants.CollectionName);
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, cancellationToken);

            await using (var migrationSession = store.CreateSession())
            {
                var migrationTransaction = await migrationSession.BeginTransactionAsync(cancellationToken);
                await QueueItemQueryPlanFixture.MigrateAsync(store, store.Configuration, migrationTransaction);
                await migrationTransaction.CommitAsync(cancellationToken);
            }

            var queueIds = QueueItemQueryPlanFixture.SampleQueueIds();
            var expected = new Dictionary<string, int>(StringComparer.Ordinal);

            await using (var seedSession = store.CreateSession())
            {
                var seedStore = new QueueItemStore(seedSession);
                var sequence = 0;

                foreach (var queueId in queueIds)
                {
                    // A different number of waiting items per queue: equal counts would pass even if the
                    // statement returned one queue's count for every queue.
                    var waiting = 1 + Array.IndexOf(queueIds, queueId);
                    expected[queueId] = waiting;

                    for (var i = 0; i < waiting; i++)
                    {
                        await seedStore.CreateAsync(NewQueueItem(queueId, sequence++, QueueItemStatus.Waiting), cancellationToken);
                    }

                    // Settled items in the same queue must not be counted.
                    await seedStore.CreateAsync(NewQueueItem(queueId, sequence++, QueueItemStatus.Completed), cancellationToken);
                }

                await seedSession.SaveChangesAsync(cancellationToken);
            }

            // Act
            await using var session = store.CreateSession();
            var queueItemStore = new QueueItemStore(session);
            var batched = await queueItemStore.CountWaitingByQueueIdsAsync(queueIds, cancellationToken);

            // Assert
            foreach (var queueId in queueIds)
            {
                var single = await queueItemStore.CountWaitingAsync(queueId, cancellationToken);
                var actual = batched.TryGetValue(queueId, out var value) ? value : 0;

                Assert.Equal(expected[queueId], single);
                Assert.Equal(expected[queueId], actual);
            }
        }
        finally
        {
            store.Dispose();

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static QueueItem NewQueueItem(string queueId, int sequence, QueueItemStatus status)
    {
        var item = new QueueItem
        {
            ItemId = $"queue-item-{sequence:D6}",
            QueueId = queueId,
            ActivityItemId = $"activity-{sequence:D6}",
            EnqueuedUtc = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc).AddSeconds(sequence),
        };

        // The status has a private setter and a guarded transition, so it is restored the way the store
        // restores a persisted item rather than assigned.
        return item.RestorePersistedStatus(status);
    }

    private static async Task<int> CountStatementsAsync(
        IStore store,
        RecordingConnectionFactory connectionFactory,
        string tableName,
        int queueCount,
        CancellationToken cancellationToken)
    {
        connectionFactory.Clear();

        await using (var session = store.CreateSession())
        {
            var queueItemStore = new QueueItemStore(session);
            var queueIds = Enumerable.Range(0, queueCount).Select(index => $"queue-{index:D4}").ToArray();
            await queueItemStore.CountWaitingByQueueIdsAsync(queueIds, cancellationToken);
        }

        return connectionFactory.Statements
            .Count(statement => statement.Contains(tableName, StringComparison.OrdinalIgnoreCase)
                && statement.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<IReadOnlyList<string>> ExplainAsync(IStore store, CancellationToken cancellationToken)
    {
        // The statement measured is the statement the store executes, read from the same builder, so the gate
        // cannot drift into proving a plan for a query only the gate runs.
        var queueIds = QueueItemQueryPlanFixture.SampleQueueIds();
        var sql = QueueItemQueries.BuildWaitingCountByQueueSql(store.Configuration, queueIds.Length);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;

        // The statement is measured verbatim. Rewriting it here — to expand a parameter, say — would mean the
        // plan describes a statement the store never sends, which is how a query that cannot execute at all on
        // an engine can still be reported as having a good plan on it.
        command.CommandText = "EXPLAIN QUERY PLAN " + sql;

        for (var index = 0; index < queueIds.Length; index++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = QueueItemQueries.QueueIdParameterName(index);
            parameter.Value = queueIds[index];
            command.Parameters.Add(parameter);
        }

        var lines = new List<string>();

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(reader.GetString(reader.FieldCount - 1));
            }
        }

        await transaction.CommitAsync(cancellationToken);

        Assert.NotEmpty(lines);

        return lines;
    }
}
