using System.Security.Claims;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.DataConnectors;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Scoring;

public sealed class SurveyGradingQueueTests : IAsyncLifetime
{
    private ScoringStore _store;

    public async ValueTask InitializeAsync()
        => _store = await ScoringStore.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task ListAsync_ForAGlobalGrader_ReadsEverySurvey()
    {
        // Act
        var query = await ListAsync(PermissionClaimsAuthorizationService.UserWith(SurveysPermissions.GradeSurveyResponses.Name), []);

        // Assert
        Assert.NotNull(query);
        Assert.Empty(query.SurveyIds);
        Assert.Equal(GradingStatus.Pending, query.Grading);
    }

    [Fact]
    public async Task ListAsync_ForAScopedGrader_ReadsOnlyTheScopedSurveys()
    {
        // Act
        var query = await ListAsync(PermissionClaimsAuthorizationService.UserWith(), ["course-quiz"]);

        // Assert
        Assert.Equal(["course-quiz"], query.SurveyIds);
    }

    [Fact]
    public async Task ListAsync_WithoutPermissionOrScope_ReadsNothing()
    {
        // Act
        var query = await ListAsync(PermissionClaimsAuthorizationService.UserWith(), []);

        // Assert
        Assert.Null(query);
    }

    private async Task<SurveyResponseQuery> ListAsync(ClaimsPrincipal user, string[] scoped)
    {
        await using var session = _store.Store.CreateSession();
        var queries = new ScoringResponseQueries(session);
        var queue = new SurveyGradingQueue(new PermissionClaimsAuthorizationService(), [new Scope(scoped)], [], new SurveyStore(session), queries);

        await queue.ListAsync(user, null, 0, 20, TestContext.Current.CancellationToken);

        return queries.LastQuery;
    }

    private sealed class Scope : ISurveyGradingScope
    {
        private readonly string[] _ids;

        public Scope(string[] ids)
        {
            _ids = ids;
        }

        public Task<IReadOnlyCollection<string>> GetGradableSurveyIdsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<string>>(_ids);
    }
}
