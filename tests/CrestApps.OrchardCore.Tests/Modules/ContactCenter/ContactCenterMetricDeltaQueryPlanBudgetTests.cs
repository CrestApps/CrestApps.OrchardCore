using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Asserts a query-plan budget for the read the metric roller issues. Replacing the read-modify-write of a
/// single daily total with an append means the contribution table is written to on every recorded event and
/// drained a batch at a time, so the batch read runs on a schedule against a table sized by traffic. If that
/// read is answered by a scan and a sort then the roller gets slower exactly as the contributions arrive
/// faster, which is the failure mode the append was introduced to remove. The results are identical either
/// way, so only a plan can see it.
/// </summary>
public sealed class ContactCenterMetricDeltaQueryPlanBudgetTests
{

    /// <summary>
    /// The alias SQLite reports for the index table in a query plan. SQLite names the query alias, not the table, so an assertion written against the physical
// table name can never match and passes no matter how the table is reached.
    /// </summary>
    private const string IndexAlias = nameof(ContactCenterEventMetricDeltaIndex);
    private const int SeededContributions = 4000;
    private const int SeededDays = 30;

    [Fact]
    public async Task RollupBatchRead_ReadsAnIndex_AndNeverSortsTheWholeBacklogToReturnOneBatch()
    {
        // Arrange & Act
        var (tableName, plan) = await MeasureAsync(
            (deltaStore, cancellationToken) => deltaStore.ListBatchAsync(500, cancellationToken));

        // Assert
        var rendered = string.Join(Environment.NewLine, plan);

        // A sort is the regression this budget exists to catch. The document query groups by document identity,
        // which the engine cannot satisfy from an ordering over the contribution columns, so any ordering added
        // to the drain makes it materialize and sort the entire backlog before it can hand back a single batch:
        // the cost of draining one batch would then grow with everything waiting rather than with the batch
        // size.
        Assert.DoesNotContain(
            plan,
            line => line.Contains("USE TEMP B-TREE FOR ORDER BY", StringComparison.OrdinalIgnoreCase));

        // The retention index leads with the append time. If the drain were answered through it the batch would
        // be taken in an order that has nothing to do with how it is consumed, and the index that exists to make
        // the purge a seek would be carrying a read it was never sized for.
        Assert.DoesNotContain(
            plan,
            line => line.Contains("IDX_ContactCenterEventMetricDeltaIndex_Retention", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            plan,
            line => line.Contains("IDX_ContactCenterEventMetricDeltaIndex_Summary", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            plan.Any(line => line.Contains(tableName, StringComparison.OrdinalIgnoreCase)),
            $"The batch read never reached the contribution table. Plan:{Environment.NewLine}{rendered}");
    }

    [Fact]
    public async Task PendingContributionRead_SeeksTheDayIndex_RatherThanScanningEveryWaitingContribution()
    {
        // Arrange
        var from = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var (tableName, plan) = await MeasureAsync(
            (deltaStore, cancellationToken) => deltaStore.ListByDateRangeAsync(from, to, cancellationToken));

        // Assert
        var rendered = string.Join(Environment.NewLine, plan);

        // This read is on the request path: a reader has to add the contributions that have not been folded yet
        // to the totals it reports. The contribution table is written to on every recorded event, so answering
        // this by walking it would make reading a summary cost more the busier the deployment is.
        Assert.DoesNotContain(
            plan,
            line => line.TrimStart().StartsWith($"SCAN {IndexAlias}", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            plan.Any(line => line.Contains("IDX_ContactCenterEventMetricDeltaIndex_Summary", StringComparison.OrdinalIgnoreCase)),
            $"The pending contribution read must seek the day index. Plan:{Environment.NewLine}{rendered}");
    }

    [Fact]
    public async Task ContributionWalk_ResumesFromItsPosition_RatherThanSortingTheTableForEveryPage()
    {
        // Arrange & Act
        var (tableName, plan) = await MeasureAsync(
            (deltaStore, cancellationToken) => deltaStore.ListContributionsAfterAsync(0, 500, cancellationToken));

        // Assert
        var rendered = string.Join(Environment.NewLine, plan);

        // The rebuild has to account for every waiting contribution, so it walks the whole table a page at a
        // time. The walk is ordered because it resumes from a position, and an ordering the engine cannot answer
        // from an index would make every page sort the entire backlog: a walk of n pages would then cost n sorts
        // of everything waiting rather than n seeks. Reading the index alone rather than the documents is what
        // makes the ordering answerable, because an index query carries no grouping by document identity.
        Assert.DoesNotContain(
            plan,
            line => line.Contains("USE TEMP B-TREE FOR ORDER BY", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            plan.Any(line => line.Contains(tableName, StringComparison.OrdinalIgnoreCase)),
            $"The contribution walk never reached the contribution table. Plan:{Environment.NewLine}{rendered}");
    }

    private static async Task<(string TableName, IReadOnlyList<string> Plan)> MeasureAsync(
        Func<ContactCenterMetricDeltaStore, CancellationToken, Task> read)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(AppContext.BaseDirectory, "QueryPlanData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"metric-delta-plan-{Guid.NewGuid():N}.db");
        var connectionFactory = new RecordingConnectionFactory($"Data Source={databasePath};Pooling=False");
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False");
            configuration.ConnectionFactory = connectionFactory;
        });

        try
        {
            store.RegisterIndexes([new ContactCenterEventMetricDeltaIndexProvider()], ContactCenterConstants.CollectionName);
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, cancellationToken);

            await using (var migrationSession = store.CreateSession())
            {
                var migrationTransaction = await migrationSession.BeginTransactionAsync(cancellationToken);
                await MigrateAsync(store.Configuration, migrationTransaction);
                await migrationTransaction.CommitAsync(cancellationToken);
            }

            await SeedAsync(store, cancellationToken);
            await AnalyzeAsync(store, cancellationToken);

            var tableName = TableName(store.Configuration);
            var plan = await ExplainAsync(store, connectionFactory, tableName, read, cancellationToken);

            return (tableName, plan);
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

    private static string TableName(IConfiguration configuration)
        => configuration.TableNameConvention.GetIndexTable(typeof(ContactCenterEventMetricDeltaIndex), ContactCenterConstants.CollectionName);

    private static async Task MigrateAsync(IConfiguration configuration, DbTransaction transaction)
    {
        var migration = new ContactCenterEventMetricDeltaIndexMigrations
        {
            SchemaBuilder = new SchemaBuilder(configuration, transaction),
        };

        await migration.CreateAsync();
    }

    private static async Task SeedAsync(IStore store, CancellationToken cancellationToken)
    {
        // Seeded through the store, not by writing index rows directly. The document query joins the index to
        // the document table, so an index populated on its own leaves the join with an empty outer side: the
        // read returns nothing, the planner sees an empty table, and every assertion about how the contribution
        // side is reached would be an artifact of that emptiness rather than a property of the query.
        var createdUtc = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        var eventTypes = new[] { "OfferAccepted", "OfferRejected", "CallEnded", "CallbackScheduled" };

        await using var session = store.CreateSession();
        var deltaStore = new ContactCenterMetricDeltaStore(session);

        for (var i = 0; i < SeededContributions; i++)
        {
            // The contributions are spread over several days and types so an ordering has something to order. A
            // table holding one day and one type is sorted by construction, which would let a plan that sorts
            // pass without ever paying for the sort.
            var day = createdUtc.Date.AddDays(-(i % SeededDays));

            await deltaStore.CreateAsync(new ContactCenterEventMetricDelta
            {
                ItemId = $"metricdelta{i:D15}",
                DateKey = ContactCenterMetricDateKey.From(day),
                Date = day,
                EventType = eventTypes[i % eventTypes.Length],
                Count = 1,
                CreatedUtc = createdUtc.AddSeconds(-i),
            }, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private static async Task AnalyzeAsync(IStore store, CancellationToken cancellationToken)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        await using (var analyze = transaction.Connection.CreateCommand())
        {
            analyze.Transaction = transaction;
            analyze.CommandText = "ANALYZE;";
            await analyze.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ExplainAsync(
        IStore store,
        RecordingConnectionFactory connectionFactory,
        string tableName,
        Func<ContactCenterMetricDeltaStore, CancellationToken, Task> read,
        CancellationToken cancellationToken)
    {
        // The statement is taken from the store as it runs rather than hand written here. The document query
        // pipeline builds it internally, so an approximation written in this file would prove a plan for a
        // query nothing executes.
        connectionFactory.Clear();

        await using (var session = store.CreateSession())
        {
            await read(new ContactCenterMetricDeltaStore(session), cancellationToken);
        }

        var execution = connectionFactory.Executions.FirstOrDefault(candidate =>
            candidate.CommandText.Contains(tableName, StringComparison.OrdinalIgnoreCase)
            && candidate.CommandText.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            execution is not null,
            $"The read issued no select against {tableName}, so there is no statement to measure a plan for.");

        await using var planSession = store.CreateSession();
        var transaction = await planSession.BeginTransactionAsync(cancellationToken);

        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "EXPLAIN QUERY PLAN " + execution.CommandText;

        foreach (var bound in execution.Parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = bound.Key;
            parameter.Value = bound.Value ?? DBNull.Value;
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
