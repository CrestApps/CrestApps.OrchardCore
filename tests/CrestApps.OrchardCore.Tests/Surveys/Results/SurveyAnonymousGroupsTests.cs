using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// The groups an output of an anonymous survey leaves out: with a filter that names a collector or a version, every
/// group that doesn't show as itself (R8-M5); and the status counts leave the hidden groups out too (R8-Mi21).
/// </summary>
public sealed class SurveyAnonymousGroupsTests : IAsyncLifetime
{
    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
        => _store = await ResultsSqliteStore.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task ConcealedGroupsAsync_OtherIsLarge_WithACollectorOrVersionFilter_LeavesOutTheMaskedGroups()
    {
        // Arrange: R8-M5. (A, v1) = 6, (B, v2) = 5, (A, v2) = Bob alone, and five one-person links on version 1, so the
        // small groups hold 6 together and Bob's row is masked ("Other"), not hidden.
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddAsync(("A", 1, 6), ("B", 2, 5), ("A", 2, 1), ("L1", 1, 1), ("L2", 1, 1), ("L3", 1, 1), ("L4", 1, 1), ("L5", 1, 1));
        await using var session = _store.OpenSession();
        var groups = Groups(session);
        var survey = Survey();

        // Act
        var unfiltered = await groups.ConcealedGroupsAsync(survey, null, cancellationToken);
        var collectorA = await groups.ConcealedGroupsAsync(survey, new SurveyResultsScope { CollectorId = "A" }, cancellationToken);
        var version2 = await groups.ConcealedGroupsAsync(survey, new SurveyResultsScope { VersionNumbers = [2] }, cancellationToken);
        var collectorAInList = await session.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == "anon" && index.CollectorId == "A", collection: SurveysConstants.ResponsesCollectionName)
            .Excluding(collectorA)
            .CountAsync(cancellationToken);

        // Assert: without a filter nothing is left out (the masked rows show as "Other"); a collector or version filter
        // leaves out Bob's masked group, the only one of collector A or of version 2 that doesn't show.
        Assert.Empty(unfiltered);
        Assert.Equal([new("anon", "A", 2, string.Empty)], collectorA);
        Assert.Equal([new("anon", "A", 2, string.Empty)], version2);
        Assert.Equal(6, collectorAInList);
    }

    [Fact]
    public async Task DecideAsync_LeavesTheHiddenGroupsOutOfTheStatusCounts()
    {
        // Arrange: R8-Mi21. Five completed and four abandoned responses on the main link, and Bob's abandoned response on
        // a link sent to him, a hidden group: counted with it, Abandoned would reach 5 and show, though the list (which
        // leaves Bob out) would then show a group of 4.
        await AddAsync(("main", 1, 5));
        await AddAsync(SurveyResponseStatus.Abandoned, ("main", 1, 4), ("for-bob", 1, 1));
        await using var session = _store.OpenSession();

        // Act
        var decision = await Groups(session).DecideAsync(Survey(), null, [], TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decision.HidesAll);
        Assert.Contains(SurveyResponseStatus.Abandoned, decision.HiddenStatuses);
    }

    private static SurveyAnonymousGroups Groups(ISession session)
        => new(new SurveyResultsQueries(session), new ServiceCollection().BuildServiceProvider());

    private static Survey Survey()
        => new() { ItemId = "anon", Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous, MinimumGroupSize = 5 } };

    private Task AddAsync(params (string CollectorId, int VersionNumber, int Count)[] groups)
        => AddAsync(SurveyResponseStatus.Completed, groups);

    private async Task AddAsync(SurveyResponseStatus status, params (string CollectorId, int VersionNumber, int Count)[] groups)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var question = ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue");

        await using var session = _store.OpenSession();

        foreach (var (collectorId, versionNumber, count) in groups)
        {
            var version = ResultsSqliteStore.Version("anon-v" + versionNumber, "anon", versionNumber, question);

            for (var index = 0; index < count; index++)
            {
                var response = ResultsSqliteStore.Response($"{status}-{collectorId}-{versionNumber}-{index}", version, status, _day);
                response.CollectorId = collectorId;
                await session.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}
