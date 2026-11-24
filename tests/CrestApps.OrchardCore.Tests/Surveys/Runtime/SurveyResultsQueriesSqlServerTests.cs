using System.Data.Common;
using System.Globalization;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Dapper;
using Microsoft.Data.SqlClient;
using YesSql;
using YesSql.Provider.SqlServer;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The results queries and the answer aggregates, which build their SQL by hand, on a real SQL Server (LocalDB): every
/// column alias is quoted for the dialect (<c>Percent</c> is a reserved word there), the counts by status and by joint key
/// read back, the hidden-groups filter applies, and the worst-case statement (a full IN list of ids plus the most hidden
/// groups) stays under SQL Server's limit of 2,100 parameters. The tests are skipped where LocalDB isn't available (Linux
/// CI), as <see cref="SurveysMigrationsSqlTests"/> are.
/// </summary>
public sealed class SurveyResultsQueriesSqlServerTests
{
    private const string LocalDbServer = @"(localdb)\MSSQLLocalDB";
    private const string LocalDbMissing = "SQL Server LocalDB is not available (it runs on Windows only).";
    private const string Enrollment = "LearningEnrollment";

    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Gets whether SQL Server LocalDB answers.
    /// </summary>
    public static bool IsLocalDbAvailable => SurveysMigrationsSqlTests.IsLocalDbAvailable;

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task ResultsQueries_OnSqlServer_ReadTheRowsCountsAndChangeTimes_AndLeaveHiddenGroupsOut()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var store = await CreateStoreAsync();
        await SeedAsync(store);
        await using var session = store.OpenSession();
        var queries = new SurveyResultsQueries(session);
        var scope = new SurveyResultsScope { SurveyId = "survey1" };
        var group = SurveyResponseGroup.Create("survey1", "A", 1, Enrollment);

        // Act
        var rows = await queries.ListRowsAsync(scope, cancellationToken);
        var byStatus = await queries.CountByStatusAsync(scope, cancellationToken);
        var byGroup = await queries.CountByGroupAsync("survey1", completedOnly: true, cancellationToken);
        var visibleRows = await queries.ListRowsAsync(new SurveyResultsScope { SurveyId = "survey1", HiddenGroups = [group] }, cancellationToken);
        var visibleByStatus = await queries.CountByStatusAsync(new SurveyResultsScope { SurveyId = "survey1", HiddenGroups = [group] }, cancellationToken);
        var activity = await queries.CountActivityAsync(["survey1"], null, null, cancellationToken);
        var pending = await queries.ListSurveysPendingGradingAsync(cancellationToken);
        var secondChange = await queries.NthEarliestChangeAsync(new SurveyResultsScope { SurveyId = "survey1", CompletedOnly = true }, 2, null, cancellationToken);
        var groupChange = await queries.NthEarliestChangeAsync(new SurveyResultsScope { SurveyId = "survey1", CompletedOnly = true }, 1, group, cancellationToken);
        var tooFew = await queries.NthEarliestChangeAsync(new SurveyResultsScope { SurveyId = "survey1", CompletedOnly = true }, 4, null, cancellationToken);
        var read = new List<string>();

        await foreach (var response in queries.ReadAsync(scope, cancellationToken))
        {
            read.Add(response.ItemId);
        }

        // Assert
        Assert.Equal(["r1", "r2", "r3", "r4"], rows.Select(row => row.ResponseId).Order(StringComparer.Ordinal));
        var first = Assert.Single(rows, row => row.ResponseId == "r1");
        Assert.Equal(80m, first.Percent);
        Assert.True(first.Passed);
        Assert.Equal(nameof(SurveyResponseStatus.Completed), first.Status);
        Assert.Equal(1, first.VersionNumber);
        Assert.Equal(_day, first.CompletedUtc);
        Assert.Equal(3, byStatus[SurveyResponseStatus.Completed]);
        Assert.Equal(1, byStatus[SurveyResponseStatus.InProgress]);
        Assert.Equal(1, byGroup[group]);
        Assert.Equal(2, byGroup[SurveyResponseGroup.Create("survey1", "collector1", 1, null)]);
        Assert.DoesNotContain(visibleRows, row => row.ResponseId == "r2");
        Assert.Equal(2, visibleByStatus[SurveyResponseStatus.Completed]);
        Assert.Equal((3L, 1L), activity["survey1"]);
        Assert.Equal(["survey1"], pending);
        Assert.Equal(_day.AddHours(1), secondChange);
        Assert.Equal(_day.AddHours(1), groupChange);
        Assert.Null(tooFew);
        Assert.Equal(["r1", "r2", "r3", "r4"], read.Order(StringComparer.Ordinal));
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task AnswerAggregates_OnSqlServer_ReadTheCountsAndStatistics()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var store = await CreateStoreAsync();
        await SeedAsync(store);
        await using var session = store.OpenSession();
        var aggregates = new SurveyAnswerAggregates(session);
        var queries = new SurveyResultsQueries(session);
        var filter = new SurveyAnswerFilter { SurveyId = "survey1" };
        var chunked = new SurveyAnswerFilter { SurveyId = "survey1", ResponseIds = ["r1", "r3"] };

        // Act
        var responses = await aggregates.CountResponsesAsync(filter, cancellationToken);
        var answered = await aggregates.CountAnsweredAsync(filter, "q1", cancellationToken);
        var choices = await aggregates.CountChoicesAsync(filter, "q1", cancellationToken);
        var statistics = await aggregates.NumberStatisticsAsync(filter, "q2", cancellationToken: cancellationToken);
        var segment = await aggregates.NumberStatisticsAsync(chunked, "q2", cancellationToken: cancellationToken);
        var red = await aggregates.ResponsesWithChoiceAsync(filter, "q1", "red", cancellationToken);
        var samples = await aggregates.TextSamplesAsync(filter, "q1", 10, cancellationToken);
        var values = await queries.ListNumberValuesAsync(filter, "q2", cancellationToken);
        var scores = await queries.ListQuestionScoresAsync(filter, cancellationToken);

        // Assert
        Assert.Equal(3, responses);
        Assert.Equal(3, answered);
        Assert.Equal(2, choices[string.Empty]["red"]);
        Assert.Equal(1, choices[string.Empty]["blue"]);
        Assert.Equal(3, statistics.Count);
        Assert.Equal(2m, statistics.Min);
        Assert.Equal(9m, statistics.Max);
        Assert.Equal(4m, statistics.Median);
        Assert.Equal(5m, Math.Round(statistics.Average ?? 0, 2));
        Assert.Equal(1, statistics.Distribution[9m]);
        Assert.Equal(2, segment.Count);
        Assert.Equal(["r1", "r2"], red.Order(StringComparer.Ordinal));
        Assert.NotNull(samples);
        Assert.Equal([2m, 4m, 9m], values.Select(value => value.Value).Order());
        Assert.NotNull(scores);
    }

    [Fact(Skip = LocalDbMissing, SkipUnless = nameof(IsLocalDbAvailable))]
    public async Task ResponseList_WithAFullInListOfSurveysAndTheMostHiddenGroups_OnSqlServer_StaysUnderTheParameterLimit()
    {
        // Arrange: R8-Mi15, the grading queue's worst case: a full chunk of survey ids, the most hidden groups one query
        // leaves out (each with a collector and a context kind, so each takes its four parameters), and every other filter.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var store = await CreateStoreAsync();
        await SeedAsync(store);
        await using var session = store.OpenSession();
        var surveyIds = Enumerable.Range(0, SurveyIdChunks.Size - 1).Select(index => "s" + index.ToString(CultureInfo.InvariantCulture)).Prepend("survey1").ToList();
        var hiddenGroups = Enumerable.Range(0, SurveyResponseGroupFilter.MaxGroups)
            .Select(index => SurveyResponseGroup.Create(surveyIds[index], "hidden-collector-" + index.ToString(CultureInfo.InvariantCulture), index + 2, "Kind" + index.ToString(CultureInfo.InvariantCulture)))
            .ToList();

        // Act
        var (count, items) = await new SurveyResponseQueries(session).ListAsync(new SurveyResponseQuery
        {
            SurveyIds = surveyIds,
            Statuses = [SurveyResponseStatus.Completed],
            Grading = GradingStatus.Pending,
            ContextKinds = [Enrollment, "LearningCourse"],
            ExcludedUserId = "grader",
            HiddenGroups = hiddenGroups,
            OldestFirst = true,
            CompletedFromUtc = _day.AddDays(-1),
            CompletedToUtc = _day.AddDays(1),
            Skip = 0,
            Take = 50,
        }, cancellationToken);
        var byStatus = await new SurveyResultsQueries(session).CountByStatusAsync(new SurveyResultsScope
        {
            SurveyId = "survey1",
            Statuses = [SurveyResponseStatus.Completed, SurveyResponseStatus.InProgress],
            HiddenGroups = [.. hiddenGroups.Select(group => group with { SurveyId = "survey1" })],
        }, cancellationToken);

        // Assert: both statements run, and the one pending enrollment response is found.
        Assert.Equal(1, count);
        Assert.Equal("r2", Assert.Single(items).ItemId);
        Assert.Equal(3, byStatus[SurveyResponseStatus.Completed]);
    }

    // Three completed responses (r2 on collector A within an enrollment, waiting for a grader), one in progress and one
    // test response, with a choice question and a number question.
    private static async Task SeedAsync(ResultsSqliteStore store)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var version = ResultsSqliteStore.Version("version1", "survey1", 1, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"), ResultsSqliteStore.NumberQuestion("q2", "score"));

        await using var session = store.OpenSession();
        await session.SaveAsync(version, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        var first = Answered("r1", version, "red", 2, "Great", _day);
        first.Percent = 80;
        first.Passed = true;
        var second = Answered("r2", version, "red", 4, "Fine", _day.AddHours(1));
        second.CollectorId = "A";
        second.Context = new RecordRef(Enrollment, "e1");
        second.Grading = GradingStatus.Pending;
        var test = Answered("r5", version, "blue", 100, "Test", _day);
        test.IsTest = true;

        foreach (var response in new[]
        {
            first,
            second,
            Answered("r3", version, "blue", 9, null, _day.AddHours(2)),
            ResultsSqliteStore.Response("r4", version, SurveyResponseStatus.InProgress, _day, ("q1", new SurveyAnswer { Values = ["blue"] })),
            test,
        })
        {
            await session.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private static SurveyResponse Answered(string id, SurveyVersion version, string color, decimal score, string comment, DateTime completed)
        => ResultsSqliteStore.Response(
            id,
            version,
            SurveyResponseStatus.Completed,
            completed,
            ("q1", new SurveyAnswer { Values = [color], OtherText = comment }),
            ("q2", new SurveyAnswer { Number = score }));

    // A results store on a new LocalDB database, dropped when the store is disposed. The tests skip through their
    // attribute before they run (IsLocalDbAvailable), never from here.
    private static async Task<ResultsSqliteStore> CreateStoreAsync()
    {
        var name = "SurveysResults_" + Guid.NewGuid().ToString("N");

        await ExecuteOnMasterAsync($"CREATE DATABASE [{name}]");

        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = LocalDbServer,
            InitialCatalog = name,
            IntegratedSecurity = true,
            Pooling = false,
            TrustServerCertificate = true,
        }.ConnectionString;

        return await ResultsSqliteStore.CreateAsync(
            configuration => configuration.UseSqlServer(connectionString),
            async () =>
            {
                try
                {
                    await ExecuteOnMasterAsync($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");
                }
                catch (SqlException)
                {
                }
            });
    }

    private static async Task ExecuteOnMasterAsync(string sql)
    {
        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = LocalDbServer,
            InitialCatalog = "master",
            IntegratedSecurity = true,
            Pooling = false,
            ConnectTimeout = 30,
            TrustServerCertificate = true,
        }.ConnectionString;

        await using DbConnection connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync(sql);
    }
}
