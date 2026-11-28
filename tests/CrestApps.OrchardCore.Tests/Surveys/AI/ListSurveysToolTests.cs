using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.AI.Tools;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Surveys.AI;

/// <summary>
/// Round 12: list_surveys finds an "own" user's older surveys (L7) and follows the small-group rule of the responses list
/// on the response count of an anonymous survey (L6).
/// </summary>
public sealed class ListSurveysToolTests : IAsyncLifetime
{
    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
        => _store = await ResultsSqliteStore.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task ListSurveys_ForAnOwnUser_FindsTheirSurveyBehindTwoHundredNewerOnes()
    {
        // Arrange: kim's survey is older than 200 surveys of other people.
        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(NewSurvey("kims", "kim", _day.AddDays(-30)), false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);

            for (var index = 0; index < 201; index++)
            {
                await write.SaveAsync(NewSurvey($"other{index}", "lee", _day.AddMinutes(index)), false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
            }

            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var session = _store.OpenSession();

        // Act
        var result = await InvokeAsync(session);

        // Assert
        Assert.Contains("\"name\":\"kims\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListSurveys_ForAnOwnUserKnownBySubjectOnly_FindsTheirSurvey()
    {
        // Arrange: kim's survey is older than 200 surveys of other people, so only a list read by kim's owner id finds
        // it; kim's principal carries the subject claim of a token and no name identifier, as an API caller's does.
        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(NewSurvey("kims", "kim", _day.AddDays(-30)), false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);

            for (var index = 0; index < 201; index++)
            {
                await write.SaveAsync(NewSurvey($"lees{index}", "lee", _day.AddMinutes(index)), false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
            }

            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var session = _store.OpenSession();

        // Act
        var result = await InvokeAsync(session, SurveyUserIds.SubjectClaimType);

        // Assert
        Assert.Contains("\"name\":\"kims\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListSurveys_OfAnAnonymousSurveyBelowItsMinimum_HidesTheResponseCount()
    {
        // Arrange: four completed responses and one in progress, under a minimum group size of five.
        await SaveAnonymousAsync(completed: 4, inProgress: 1);
        await using var session = _store.OpenSession();

        // Act
        var result = await InvokeAsync(session);

        // Assert
        using var document = JsonDocument.Parse(result);
        var survey = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(JsonValueKind.Null, survey.GetProperty("responses").ValueKind);
    }

    [Fact]
    public async Task ListSurveys_OfAnAnonymousSurvey_CountsOnlyTheStatusesTheListShows()
    {
        // Arrange: five completed responses (the minimum) and one lone screened-out response, which the list leaves out.
        await SaveAnonymousAsync(completed: 5, inProgress: 0, screenedOut: 1);
        await using var session = _store.OpenSession();

        // Act
        var result = await InvokeAsync(session);

        // Assert
        using var document = JsonDocument.Parse(result);
        var survey = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(5, survey.GetProperty("responses").GetInt32());
    }

    private static Survey NewSurvey(string id, string ownerId, DateTime modifiedUtc)
        => new()
        {
            ItemId = id,
            Name = id,
            Title = id,
            OwnerId = ownerId,
            Status = SurveyStatus.Open,
            CreatedUtc = modifiedUtc,
            ModifiedUtc = modifiedUtc,
        };

    private async Task SaveAnonymousAsync(int completed, int inProgress, int screenedOut = 0)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var version = ResultsSqliteStore.Version("anon-v1", "anon", 1, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"));
        var survey = NewSurvey("anon", "kim", _day);
        survey.PublishedVersionId = version.ItemId;
        survey.PublishedVersionNumber = 1;
        survey.Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous, MinimumGroupSize = 5 };

        await using var session = _store.OpenSession();
        await session.SaveAsync(survey, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(version, false, SurveysConstants.CollectionName, cancellationToken);

        var statuses = Enumerable.Repeat(SurveyResponseStatus.Completed, completed)
            .Concat(Enumerable.Repeat(SurveyResponseStatus.InProgress, inProgress))
            .Concat(Enumerable.Repeat(SurveyResponseStatus.ScreenedOut, screenedOut))
            .ToList();

        for (var index = 0; index < statuses.Count; index++)
        {
            var response = ResultsSqliteStore.Response($"anon-r{index}", version, statuses[index], _day, ("q1", new SurveyAnswer { Values = ["red"] }));
            await session.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    // kim has "manage own surveys" only: the survey authorization grants the surveys kim owns, nothing site-wide.
    private static async Task<string> InvokeAsync(ISession session, string userIdClaimType = ClaimTypes.NameIdentifier)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(userIdClaimType, "kim")], "Test"));
        var surveyStore = new SurveyStore(session);
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == SurveysPermissions.ManageOwnSurveys.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } })
            .AddSingleton(new SurveyListService(session, new SurveyIndexReader(session, surveyStore)))
            .AddSingleton<ISurveyAuthorizationService>(new OwnerOnly())
            .AddSingleton<ISurveyResponseQueries>(new SurveyResponseQueries(session))
            .AddSingleton(authorizationService.Object)
            .AddSingleton(new SurveyAnonymousGroups(new SurveyResultsQueries(session), new ServiceCollection().BuildServiceProvider()))
            .BuildServiceProvider();

        var result = await new ListSurveysTool().InvokeAsync(new AIFunctionArguments { Services = services }, TestContext.Current.CancellationToken);

        return Assert.IsType<string>(result);
    }

    private sealed class OwnerOnly : ISurveyAuthorizationService
    {
        public Task<bool> AuthorizeAsync(ClaimsPrincipal user, Survey survey, SurveyOperation operation, CancellationToken cancellationToken = default)
            => Task.FromResult(operation == SurveyOperation.Manage && survey?.OwnerId == SurveyUserIds.Of(user));
    }
}
