using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Asserts a query-plan and result budget for the read the agent session cleanup pass issues every minute.
/// The pass exists to sign out agents whose browser stopped sending heartbeats, so it runs on a schedule
/// against a table holding a row per agent who has ever connected. Two regressions in that read return exactly
/// the same sessions and are therefore invisible to any functional test: answering the heartbeat cut-off by
/// walking the table rather than seeking the index built for it, and returning every stale session at once
/// when a restart makes every session stale simultaneously.
/// </summary>
public sealed class AgentSessionQueryPlanBudgetTests
{
    private const int SeededSessions = 4000;

    [Fact]
    public async Task StaleSessionRead_SeeksTheHeartbeatIndex_RatherThanWalkingEverySessionEverOpened()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(AppContext.BaseDirectory, "QueryPlanData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"agent-session-plan-{Guid.NewGuid():N}.db");
        var connectionFactory = new RecordingConnectionFactory($"Data Source={databasePath};Pooling=False");
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False");
            configuration.ConnectionFactory = connectionFactory;
        });

        try
        {
            store.RegisterIndexes([new AgentSessionIndexProvider()], ContactCenterStorage.CollectionName);
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);

            await using (var migrationSession = store.CreateSession())
            {
                var migrationTransaction = await migrationSession.BeginTransactionAsync(cancellationToken);
                await MigrateAsync(store, store.Configuration, migrationTransaction);
                await migrationTransaction.CommitAsync(cancellationToken);
            }

            await SeedAsync(store, cancellationToken);
            await AnalyzeAsync(store, cancellationToken);

            var tableName = TableName(store.Configuration);

            // Act
            var plan = await ExplainAsync(store, connectionFactory, tableName, cancellationToken);

            // Assert
            var rendered = string.Join(Environment.NewLine, plan);

            // Reading the table end to end is the regression. The cut-off is a range over the heartbeat time,
            // which the retention index leads with, so the sessions older than it can be reached without
            // touching the ones that are not — and the table holds a row for every agent who has ever
            // connected, so the difference grows with the deployment rather than with the number of sessions
            // that actually went stale.
            // Asserted on the plan's own verb rather than on the table name. SQLite names the query alias, not
            // the table, so an assertion written against the physical table name can never match and would pass
            // no matter how the table is reached.
            Assert.DoesNotContain(
                plan,
                line => line.TrimStart().StartsWith($"SCAN {nameof(AgentSessionIndex)}", StringComparison.OrdinalIgnoreCase));

            Assert.True(
                plan.Any(line => line.Contains("IDX_AgentSessionIndex_Retention", StringComparison.OrdinalIgnoreCase)),
                $"The stale session read must seek the heartbeat index. Plan:{Environment.NewLine}{rendered}");

            // The bound is only worth having if the engine can stop once it has filled the page. An ordering the
            // index cannot answer makes it materialize and sort every stale session first, so a restart that
            // makes the whole tenant stale at once costs the same as it did unbounded.
            Assert.DoesNotContain(
                plan,
                line => line.Contains("TEMP B-TREE", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task StaleSessionRead_IsBounded_SoOneRestartDoesNotHandTheCleanupPassEverySessionAtOnce()
    {
        // Arrange
        // Every session in the tenant goes stale together whenever a deployment drops every connection at once,
        // and the caller takes a distributed lock, re-reads and deletes for each session it is handed. An
        // unbounded read turns that single event into one pass doing all of it while the next pass is already
        // due.
        var cancellationToken = TestContext.Current.CancellationToken;
        var databasePath = Path.Combine(Path.GetTempPath(), $"agent-session-bound-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        try
        {
            store.RegisterIndexes([new AgentSessionIndexProvider()], ContactCenterStorage.CollectionName);
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);

            await using (var migrationSession = store.CreateSession())
            {
                var migrationTransaction = await migrationSession.BeginTransactionAsync(cancellationToken);
                await MigrateAsync(store, store.Configuration, migrationTransaction);
                await migrationTransaction.CommitAsync(cancellationToken);
            }

            await SeedAsync(store, cancellationToken);

            // Act
            await using var session = store.CreateSession();
            var stale = await new AgentSessionStore(session).GetStaleAsync(_cutoffUtc, cancellationToken);

            // Assert
            // More sessions are stale than the bound allows, so a read that returns exactly the bound is the
            // only result that proves the bound is applied rather than simply never reached.
            Assert.Equal(AgentSessionStore.MaxStaleSessionsPerPass, stale.Count);

            // A bounded read only drains if it takes the oldest heartbeats first. Returning an arbitrary page
            // leaves which sessions a pass expires up to the engine, so an agent whose heartbeat stopped can sit
            // unexpired behind a page that keeps being answered with someone else while the caller is told it is
            // working. Every session handed back must therefore be at least as stale as every one held over.
            var returnedNewest = stale.Max(session => session.LastHeartbeatUtc);
            var returnedUserIds = stale.Select(session => session.UserId).ToHashSet(StringComparer.Ordinal);

            await using var allSessionsSession = store.CreateSession();
            var everyStale = await new AgentSessionStore(allSessionsSession).GetByUserIdsAsync(
                _staleUserIds,
                cancellationToken);
            var heldOverOldest = everyStale
                .Where(session => !returnedUserIds.Contains(session.UserId))
                .Min(session => session.LastHeartbeatUtc);

            Assert.True(
                returnedNewest <= heldOverOldest,
                $"The bounded read must take the oldest heartbeats first. Newest returned {returnedNewest:O} is later than the oldest held over {heldOverOldest:O}.");
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static readonly DateTime _seedHeartbeatUtc = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _cutoffUtc = new(2026, 7, 16, 11, 0, 0, DateTimeKind.Utc);

    private static readonly string[] _staleUserIds = Enumerable.Range(0, SeededSessions)
        .Where(index => index % 3 != 0)
        .Select(index => $"user{index:D21}")
        .ToArray();

    private static string TableName(IConfiguration configuration)
        => configuration.TableNameConvention.GetIndexTable(typeof(AgentSessionIndex), ContactCenterStorage.CollectionName);

    private static async Task MigrateAsync(IStore store, IConfiguration configuration, DbTransaction transaction)
    {
        var migration = new AgentSessionIndexMigrations(store)
        {
            SchemaBuilder = new SchemaBuilder(configuration, transaction),
        };

        await migration.CreateAsync();
    }

    private static async Task SeedAsync(IStore store, CancellationToken cancellationToken)
    {
        // Seeded through the store rather than by writing index rows directly, because the read is a document
        // query: an index populated on its own leaves the join with an empty outer side, the read returns
        // nothing, and every assertion about how the session table is reached becomes an artifact of that
        // emptiness rather than a property of the query.
        await using var session = store.CreateSession();
        var sessionStore = new AgentSessionStore(session);

        for (var i = 0; i < SeededSessions; i++)
        {
            // Two thirds of the sessions are older than the cut-off and one third is newer, so the planner has a
            // range worth seeking rather than a table where every row qualifies.
            var heartbeatUtc = i % 3 == 0
                ? _seedHeartbeatUtc
                : _cutoffUtc.AddSeconds(-i);

            await sessionStore.CreateAsync(new AgentSession
            {
                ItemId = IdGenerator.GenerateId(),
                UserId = $"user{i:D21}",
                IsOnline = true,
                LastHeartbeatUtc = heartbeatUtc,
                CreatedUtc = _seedHeartbeatUtc,
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
        CancellationToken cancellationToken)
    {
        // The statement is taken from the store as it runs rather than written here. The document query
        // pipeline builds it internally, so an approximation would measure a plan for a query nothing executes.
        connectionFactory.Clear();

        await using (var readSession = store.CreateSession())
        {
            await new AgentSessionStore(readSession).GetStaleAsync(_cutoffUtc, cancellationToken);
        }

        // The statement under budget is the one that chooses which sessions are stale, not the bounded fetch of
        // the documents behind them. That first statement reads the index alone, so it is identified by naming
        // the index table without joining the document table.
        var execution = connectionFactory.Executions.FirstOrDefault(candidate =>
            candidate.CommandText.Contains(tableName, StringComparison.OrdinalIgnoreCase)
            && !candidate.CommandText.Contains("_Document", StringComparison.OrdinalIgnoreCase)
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
