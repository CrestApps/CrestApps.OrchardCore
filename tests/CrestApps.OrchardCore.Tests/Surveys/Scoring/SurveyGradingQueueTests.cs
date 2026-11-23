using System.Security.Claims;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.DataConnectors;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    [Fact]
    public async Task ListAsync_WholeQueue_LeavesOutKindsGradedOnTheirOwnersPage()
    {
        // Act
        var query = await ListAsync(PermissionClaimsAuthorizationService.UserWith(SurveysPermissions.GradeSurveyResponses.Name), [], OwnQueueKinds());

        // Assert
        Assert.Equal(["Enrollment"], query.ExcludedContextKinds);
    }

    [Fact]
    public async Task ListAsync_OneSurvey_KeepsEveryContextKind()
    {
        // Act
        var query = await ListAsync(PermissionClaimsAuthorizationService.UserWith(SurveysPermissions.GradeSurveyResponses.Name), [], OwnQueueKinds(), "quiz");

        // Assert
        Assert.Empty(query.ExcludedContextKinds);
        Assert.Equal(["quiz"], query.SurveyIds);
    }

    [Fact]
    public async Task ListAsync_LeavesOutTheGradersOwnResponses()
    {
        // Arrange
        var user = PermissionClaimsAuthorizationService.UserWith(SurveysPermissions.GradeSurveyResponses.Name);
        user.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "grader-id")]));

        // Act
        var query = await ListAsync(user, []);

        // Assert
        Assert.Equal("grader-id", query.ExcludedUserId);
    }

    [Fact]
    public async Task ListAsync_LeavesOutAnAnonymousSurveyBelowItsMinimum_AndReadsTheRestInRandomIdOrder()
    {
        // Arrange: an anonymous survey with one response to grade, another with five, and an identified one.
        await SavePendingAsync("few", AnonymityMode.Anonymous, 1);
        await SavePendingAsync("many", AnonymityMode.Anonymous, 5);
        await SavePendingAsync("named", AnonymityMode.Identified, 1);

        // Act
        var query = await ListAsync(PermissionClaimsAuthorizationService.UserWith(SurveysPermissions.GradeSurveyResponses.Name), []);

        // Assert
        Assert.Equal(["few"], query.ExcludedSurveyIds);
        Assert.True(query.OrderById);
    }

    [Fact]
    public void IsOwn_MatchesTheSignedInUsersResponseOnly()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u1")], "test"));

        // Act and assert
        Assert.True(SurveyOwnWork.IsOwn(user, new SurveyResponse { UserId = "u1" }));
        Assert.False(SurveyOwnWork.IsOwn(user, new SurveyResponse { UserId = "u2" }));
        Assert.False(SurveyOwnWork.IsOwn(user, new SurveyResponse()));
        Assert.False(SurveyOwnWork.IsOwn(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u1")])), new SurveyResponse { UserId = "u1" }));
    }

    private async Task SavePendingAsync(string surveyId, AnonymityMode anonymity, int count)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _store.Store.CreateSession();
        await session.SaveAsync(new Survey
        {
            ItemId = surveyId,
            Name = surveyId,
            Title = surveyId,
            Status = SurveyStatus.Open,
            Access = new SurveyAccessSettings { Anonymity = anonymity, MinimumGroupSize = 5 },
        }, false, SurveysConstants.CollectionName, cancellationToken);

        for (var index = 0; index < count; index++)
        {
            await session.SaveAsync(new SurveyResponse
            {
                ItemId = $"{surveyId}-{index}",
                SurveyId = surveyId,
                Status = SurveyResponseStatus.Completed,
                Grading = GradingStatus.Pending,
                StartedUtc = DateTime.UtcNow,
                CompletedUtc = DateTime.UtcNow,
            }, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private static SurveyContextKindOptions OwnQueueKinds()
    {
        var options = new SurveyContextKindOptions();
        options.Kinds["Enrollment"] = new SurveyContextKindDescriptor { Kind = "Enrollment", HasOwnGradingQueue = true };
        options.Kinds["Course"] = new SurveyContextKindDescriptor { Kind = "Course" };

        return options;
    }

    private async Task<SurveyResponseQuery> ListAsync(ClaimsPrincipal user, string[] scoped, SurveyContextKindOptions kinds = null, string surveyId = null)
    {
        await using var session = _store.Store.CreateSession();
        var queries = new ScoringResponseQueries(session);
        var results = new SurveyResultsQueries(session);
        var queue = new SurveyGradingQueue(new PermissionClaimsAuthorizationService(), [new Scope(scoped)], [], new SurveyStore(session), queries, Options.Create(kinds ?? new SurveyContextKindOptions()), results, new SurveyAnonymousGroups(results, new ServiceCollection().BuildServiceProvider()));

        await queue.ListAsync(user, surveyId, 0, 20, TestContext.Current.CancellationToken);

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
