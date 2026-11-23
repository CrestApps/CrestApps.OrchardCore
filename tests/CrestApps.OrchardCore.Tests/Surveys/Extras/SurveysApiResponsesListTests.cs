using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Api;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Surveys.Extras;

/// <summary>
/// The API's responses list follows the responses list's small-group rule on anonymous surveys (P3-L1) and shows no
/// site-wide response number there (P2-H1).
/// </summary>
public sealed class SurveysApiResponsesListTests : IAsyncLifetime
{
    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
        => _store = await ResultsSqliteStore.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task ListResponses_OfAnAnonymousSurvey_LeavesOutALoneScreenedOutResponseAndItsNumber()
    {
        // Arrange: five completed responses (the minimum) and one screened out.
        await SaveAsync(completed: 5, screenedOut: 1);
        await using var session = _store.OpenSession();

        // Act
        var json = await ListAsync(session);

        // Assert
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(5, json.GetProperty("count").GetInt32());
        Assert.Equal(5, items.Count);
        Assert.All(items, item => Assert.Equal(nameof(SurveyResponseStatus.Completed), item.GetProperty("status").GetString()));
        Assert.All(items, item => Assert.DoesNotContain("R-", item.GetProperty("number").GetString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListResponses_OfAnAnonymousSurveyBelowItsMinimum_IsHidden()
    {
        // Arrange
        await SaveAsync(completed: 4, screenedOut: 5);
        await using var session = _store.OpenSession();

        // Act
        var json = await ListAsync(session);

        // Assert
        Assert.True(json.GetProperty("hidden").GetBoolean());
        Assert.Empty(json.GetProperty("items").EnumerateArray());
    }

    private static async Task<JsonElement> ListAsync(ISession session)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var requestServices = new ServiceCollection()
            .AddSingleton(new SurveyRespondentNames(services, RecipeRoundTrip.Localizer<SurveyRespondentNames>()))
            .AddSingleton(new SurveyAnonymousGroups(new SurveyResultsQueries(session), services))
            .BuildServiceProvider();
        var http = new DefaultHttpContext
        {
            RequestServices = requestServices,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "kim")], "Test")),
        };

        var result = await SurveysApiEndpoints.ListResponsesAsync("anon", null, null, http, new Allow(), new SurveyStore(session), new SurveyResponseQueries(session));
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;

        return JsonSerializer.SerializeToElement(value);
    }

    private async Task SaveAsync(int completed, int screenedOut)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var version = ResultsSqliteStore.Version("anon-v1", "anon", 1, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"));

        await using var session = _store.OpenSession();
        await session.SaveAsync(new Survey
        {
            ItemId = "anon",
            Name = "anon",
            Title = "anon",
            Status = SurveyStatus.Open,
            PublishedVersionId = version.ItemId,
            PublishedVersionNumber = 1,
            Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous, MinimumGroupSize = 5 },
        }, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(version, false, SurveysConstants.CollectionName, cancellationToken);

        for (var index = 0; index < completed + screenedOut; index++)
        {
            var status = index < completed ? SurveyResponseStatus.Completed : SurveyResponseStatus.ScreenedOut;
            var response = ResultsSqliteStore.Response($"anon-r{index}", version, status, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
            response.Number = "R-" + index;
            await session.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private sealed class Allow : ISurveyAuthorizationService
    {
        public Task<bool> AuthorizeAsync(ClaimsPrincipal user, Survey survey, SurveyOperation operation, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
