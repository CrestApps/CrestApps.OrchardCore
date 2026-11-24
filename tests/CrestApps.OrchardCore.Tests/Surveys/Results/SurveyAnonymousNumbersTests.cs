using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// R8-Mi18: clearing the numbers of a survey that becomes anonymous goes through the response store a page at a time.
/// </summary>
public sealed class SurveyAnonymousNumbersTests : IAsyncLifetime
{
    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
        => _store = await ResultsSqliteStore.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task ClearAsync_ClearsEveryNumberOfTheSurvey_PastOnePage_AndNoOtherSurveys()
    {
        // Arrange: more numbered responses than one page, and one of another survey.
        var cancellationToken = TestContext.Current.CancellationToken;
        var question = ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue");
        var version = ResultsSqliteStore.Version("s1-v1", "s1", 1, question);
        var other = ResultsSqliteStore.Version("s2-v1", "s2", 1, question);
        var total = SurveysConstants.PageSize + 3;

        await using (var write = _store.OpenSession())
        {
            for (var index = 0; index < total; index++)
            {
                var response = ResultsSqliteStore.Response("r" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), version, SurveyResponseStatus.Completed, _day);
                response.Number = "R" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await write.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            }

            var kept = ResultsSqliteStore.Response("kept", other, SurveyResponseStatus.Completed, _day);
            kept.Number = "R-kept";
            await write.SaveAsync(kept, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
        }

        // Act
        int cleared;

        await using (var session = _store.OpenSession())
        {
            cleared = await SurveyAnonymousNumbers.ClearAsync(new SurveyResponseStore(session), "s1", cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var read = _store.OpenSession();
        var numbered = await read.QueryIndex<SurveyResponseIndex>(index => index.Number != null, collection: SurveysConstants.ResponsesCollectionName).ListAsync(cancellationToken);
        var documents = (await read.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == "s1", collection: SurveysConstants.ResponsesCollectionName).ListAsync(cancellationToken)).ToList();

        Assert.Equal(total, cleared);
        Assert.Equal("kept", Assert.Single(numbered).ItemId);
        Assert.Equal(total, documents.Count);
        Assert.All(documents, response => Assert.Null(response.Number));
    }
}
