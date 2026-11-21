using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// The answer index and the aggregates over it, on a real SQLite store.
/// </summary>
public sealed class SurveyAnswerAggregatesTests : IAsyncLifetime
{
    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _store = await ResultsSqliteStore.CreateAsync();

        var version = ResultsSqliteStore.Version("version1", "survey1", 1, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"), ResultsSqliteStore.NumberQuestion("q2", "score"));

        await using var session = _store.OpenSession();
        await session.SaveAsync(version, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        await Save(session, Answered("r1", version, "red", 2, "Great", _day));
        await Save(session, Answered("r2", version, "red", 4, "Fine", _day.AddHours(1)));
        await Save(session, Answered("r3", version, "blue", 9, null, _day.AddHours(2)));

        // Neither an unfinished response nor a test response is counted.
        await Save(session, ResultsSqliteStore.Response("r4", version, SurveyResponseStatus.InProgress, _day, ("q1", new SurveyAnswer { Values = ["blue"] })));
        var test = Answered("r5", version, "blue", 100, "Test", _day);
        test.IsTest = true;
        await Save(session, test);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task AnswerIndex_HasRowsOnlyForCompletedNonTestResponses()
    {
        // Arrange
        await using var session = _store.OpenSession();

        // Act
        var rows = await session.QueryIndex<SurveyAnswerIndex>(collection: SurveysConstants.ResponsesCollectionName).ListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["r1", "r2", "r3"], rows.Select(row => row.ResponseId).Distinct().Order(StringComparer.Ordinal));
        Assert.All(rows, row => Assert.Equal(1, row.VersionNumber));
    }

    [Fact]
    public async Task CountChoicesAsync_CountsEachChoice()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var aggregates = new SurveyAnswerAggregates(session);

        // Act
        var counts = await aggregates.CountChoicesAsync(Filter(), "q1", TestContext.Current.CancellationToken);

        // Assert
        var choices = counts[string.Empty];
        Assert.Equal(2, choices["red"]);
        Assert.Equal(1, choices["blue"]);
    }

    [Fact]
    public async Task CountResponsesAndAnswered_CountCompletedResponses()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var aggregates = new SurveyAnswerAggregates(session);

        // Act
        var responses = await aggregates.CountResponsesAsync(Filter(), TestContext.Current.CancellationToken);
        var answered = await aggregates.CountAnsweredAsync(Filter(), "q2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, responses);
        Assert.Equal(3, answered);
    }

    [Fact]
    public async Task NumberStatisticsAsync_ComputesAverageMedianAndDistribution()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var aggregates = new SurveyAnswerAggregates(session);

        // Act
        var statistics = await aggregates.NumberStatisticsAsync(Filter(), "q2", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, statistics.Count);
        Assert.Equal(5m, statistics.Average);
        Assert.Equal(4m, statistics.Median);
        Assert.Equal(2m, statistics.Min);
        Assert.Equal(9m, statistics.Max);
        Assert.Equal(3, statistics.Distribution.Count);
    }

    [Fact]
    public async Task NumberStatisticsAsync_ForASegment_SummarizesOnlyItsResponses()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var aggregates = new SurveyAnswerAggregates(session);
        var red = await aggregates.ResponsesWithChoiceAsync(Filter(), "q1", "red", TestContext.Current.CancellationToken);
        var filter = Filter();
        filter.ResponseIds = [.. red, .. Enumerable.Range(0, 700).Select(index => $"missing{index}")];

        // Act
        var statistics = await aggregates.NumberStatisticsAsync(filter, "q2", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["r1", "r2"], red.Order(StringComparer.Ordinal));
        Assert.Equal(2, statistics.Count);
        Assert.Equal(3m, statistics.Median);
    }

    [Fact]
    public async Task TextSamplesAsync_ReturnsTheLatestTexts()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var aggregates = new SurveyAnswerAggregates(session);

        // Act
        var samples = await aggregates.TextSamplesAsync(Filter(), "q1", 1, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Fine"], samples);
    }

    [Fact]
    public async Task Aggregates_WithAnEmptySegment_MatchNothing()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var aggregates = new SurveyAnswerAggregates(session);
        var filter = Filter();
        filter.ResponseIds = [];

        // Act
        var responses = await aggregates.CountResponsesAsync(filter, TestContext.Current.CancellationToken);
        var counts = await aggregates.CountChoicesAsync(filter, "q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, responses);
        Assert.Empty(counts);
    }

    private static SurveyAnswerFilter Filter()
        => new()
        {
            SurveyId = "survey1",
        };

    private static SurveyResponse Answered(string id, SurveyVersion version, string color, decimal score, string comment, DateTime completed)
        => ResultsSqliteStore.Response(
            id,
            version,
            SurveyResponseStatus.Completed,
            completed,
            ("q1", new SurveyAnswer { Values = [color], OtherText = comment }),
            ("q2", new SurveyAnswer { Number = score }));

    private static Task Save(ISession session, SurveyResponse response)
        => session.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, TestContext.Current.CancellationToken);
}
