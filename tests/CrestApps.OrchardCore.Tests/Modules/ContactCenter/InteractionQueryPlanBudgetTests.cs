using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Asserts a query-plan budget for the reservation path. Routing asks how much live work an agent is already
/// holding before every offer, so if that question is answered by reading the interaction table end to end then
/// the cost of a routing decision grows with everything the contact center has ever recorded. The query returns
/// identical results either way, so no functional test can see the difference: a plan is the only evidence, and
/// without it the regression stays invisible until the table is large enough to be a production incident.
/// </summary>
public sealed class InteractionQueryPlanBudgetTests
{

    /// <summary>
    /// The alias SQLite reports for the index table in a query plan. SQLite names the query alias, not the table, so an assertion written against the physical
// table name can never match and passes no matter how the table is reached.
    /// </summary>
    private const string IndexAlias = nameof(InteractionIndex);
    [Fact]
    public async Task ActiveCountByAgent_SeeksAnIndexInsteadOfScanningTheInteractionTable()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(AppContext.BaseDirectory, "QueryPlanData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"query-plan-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        try
        {
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, cancellationToken);

            await using (var seedSession = store.CreateSession())
            {
                var seedTransaction = await seedSession.BeginTransactionAsync(cancellationToken);
                await InteractionQueryPlanFixture.MigrateAsync(store.Configuration, seedTransaction);
                await InteractionQueryPlanFixture.SeedAsync(store.Configuration, seedTransaction, cancellationToken);

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
            // SQLite reports reading a table end to end as SCAN and an index seek as SEARCH, so the budget is
            // that no step of the plan scans the interaction table.
            var tableName = InteractionQueryPlanFixture.TableName(store.Configuration);
            var rendered = string.Join(Environment.NewLine, plan);

            Assert.DoesNotContain(
                plan,
                line => line.TrimStart().StartsWith($"SCAN {IndexAlias}", StringComparison.OrdinalIgnoreCase));

            Assert.True(
                plan.Any(line => line.Contains("IDX_InteractionIndex_ActiveByAgent", StringComparison.OrdinalIgnoreCase)),
                $"The reservation path must seek IDX_InteractionIndex_ActiveByAgent. Plan:{Environment.NewLine}{rendered}");

            // Both columns must be seek constraints, not just the leading one. An exclusive chain of inequalities
            // on the status still seeks the index by agent, but it then has to walk every interaction that agent
            // has ever handled to test each one, so the work still grows without bound while the plan still
            // names the index. Requiring the status in the seek is what distinguishes the two.
            Assert.True(
                plan.Any(line => line.Contains("AgentId=?", StringComparison.OrdinalIgnoreCase)
                    && line.Contains("Status=?", StringComparison.OrdinalIgnoreCase)),
                $"The reservation path must constrain both the agent and the status through the index rather than filtering the status per row. Plan:{Environment.NewLine}{rendered}");

            // A covering index answers the count from the index alone. Without it every matching row costs a
            // second read of the table to fetch columns the index does not carry.
            Assert.True(
                plan.Any(line => line.Contains("COVERING INDEX", StringComparison.OrdinalIgnoreCase)),
                $"The reservation path must be answered from the index alone. Plan:{Environment.NewLine}{rendered}");
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task ReservationPathLinqQueries_SeekAnIndexInsteadOfScanningTheInteractionTable()
    {
        // The single-agent reservation check and the agent's active-interaction lookup are expressed through the
        // document query pipeline rather than as hand-written SQL, so their statements are built somewhere this
        // repository never writes down. Measuring only the hand-written statement would leave the predicate on
        // these paths free to revert to a form no index can answer while every gate stayed green.
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(AppContext.BaseDirectory, "QueryPlanData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"query-plan-linq-{Guid.NewGuid():N}.db");
        var connectionFactory = new RecordingConnectionFactory($"Data Source={databasePath};Pooling=False");
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False");
            configuration.ConnectionFactory = connectionFactory;
        });

        try
        {
            store.RegisterIndexes([new InteractionIndexProvider()]);
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, cancellationToken);

            await using (var seedSession = store.CreateSession())
            {
                var seedTransaction = await seedSession.BeginTransactionAsync(cancellationToken);
                await InteractionQueryPlanFixture.MigrateAsync(store.Configuration, seedTransaction);
                await InteractionQueryPlanFixture.SeedAsync(store.Configuration, seedTransaction, cancellationToken);

                await using (var analyze = seedTransaction.Connection.CreateCommand())
                {
                    analyze.Transaction = seedTransaction;
                    analyze.CommandText = "ANALYZE;";
                    await analyze.ExecuteNonQueryAsync(cancellationToken);
                }

                await seedTransaction.CommitAsync(cancellationToken);
            }

            var tableName = InteractionQueryPlanFixture.TableName(store.Configuration);

            // Act
            connectionFactory.Clear();

            await using (var querySession = store.CreateSession())
            {
                var interactionStore = new InteractionStore(querySession);
                await interactionStore.CountActiveByAgentAsync("agent-0001", cancellationToken);
                await interactionStore.FindActiveByAgentAsync("agent-0001", cancellationToken);
            }

            var measured = connectionFactory.Statements
                .Where(statement => statement.Contains(tableName, StringComparison.OrdinalIgnoreCase)
                    && statement.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            // Assert
            // Two statements are expected — the count and the lookup. Fewer means the recording missed one and
            // the assertions below would be vacuous.
            Assert.Equal(2, measured.Length);

            foreach (var statement in measured)
            {
                var plan = await ExplainRecordedAsync(store, statement, cancellationToken);
                var rendered = string.Join(Environment.NewLine, plan);

                Assert.DoesNotContain(
                    plan,
                    line => line.TrimStart().StartsWith($"SCAN {IndexAlias}", StringComparison.OrdinalIgnoreCase));

                Assert.True(
                    plan.Any(line => line.Contains("AgentId=?", StringComparison.OrdinalIgnoreCase)
                        && line.Contains("Status=?", StringComparison.OrdinalIgnoreCase)),
                    $"A reservation-path query must constrain both the agent and the status through an index rather than filtering the status per row.{Environment.NewLine}{statement}{Environment.NewLine}Plan:{Environment.NewLine}{rendered}");
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public void OccupyingAgentStatuses_CoverEveryStatusThatIsNotSettledOrPending()
    {
        // A declared inclusive set is a correctness risk an exclusive chain of inequalities does not have: adding
        // a status to the enum silently drops it out of "occupying", so an agent holding work in the new status
        // looks idle and is handed more. This gate forces whoever adds a status to classify it.
        var all = Enum.GetValues<InteractionStatus>().ToHashSet();
        var partitioned = new List<InteractionStatus>();
        partitioned.AddRange(InteractionStatuses.OccupyingAgent);
        partitioned.AddRange(InteractionStatuses.Settled);
        partitioned.Add(InteractionStatus.Created);

        Assert.Equal(partitioned.Count, partitioned.Distinct().Count());
        Assert.Equal(all, partitioned.ToHashSet());
    }

    [Fact]
    public void UnsettledStatuses_AreExactlyTheStatusesThatAreNotSettled()
    {
        var all = Enum.GetValues<InteractionStatus>().ToHashSet();
        var partitioned = new List<InteractionStatus>();
        partitioned.AddRange(InteractionStatuses.Unsettled);
        partitioned.AddRange(InteractionStatuses.Settled);

        Assert.Equal(partitioned.Count, partitioned.Distinct().Count());
        Assert.Equal(all, partitioned.ToHashSet());
    }

    private static async Task<IReadOnlyList<string>> ExplainRecordedAsync(IStore store, string sql, CancellationToken cancellationToken)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "EXPLAIN QUERY PLAN " + sql;

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

    private static async Task<IReadOnlyList<string>> ExplainAsync(IStore store, CancellationToken cancellationToken)
    {
        // The statement measured is the statement the store executes, read from the same builder, so the gate
        // cannot drift into proving a plan for a query only the gate runs.
        var agentIds = InteractionQueryPlanFixture.SampleAgentIds();
        var sql = InteractionQueries.BuildActiveCountByAgentSql(store.Configuration, agentIds.Length);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;

        // The statement is measured verbatim. Rewriting it here — to expand a parameter, say — would mean the
        // plan describes a statement the store never sends, which is how a query that cannot execute at all on
        // an engine can still be reported as having a good plan on it.
        command.CommandText = "EXPLAIN QUERY PLAN " + sql;

        for (var index = 0; index < agentIds.Length; index++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = InteractionQueries.AgentIdParameterName(index);
            parameter.Value = agentIds[index];
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
