using System.Security.Claims;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.DataSources;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Reports;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// The Surveys data source on a real SQLite store: which data sets a person sees, which rows they read, and when the
/// respondent is shown.
/// </summary>
public sealed class SurveysDataSourceTests : IAsyncLifetime
{
    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _store = await ResultsSqliteStore.CreateAsync();

        var identified = Version("v1", "survey1");
        var anonymous = Version("v2", "survey2");

        await using var session = _store.OpenSession();
        await session.SaveAsync(Survey("survey1", "csat", "v1", "pat-id", AnonymityMode.Identified), false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(Survey("survey2", "pulse", "v2", "lee-id", AnonymityMode.Anonymous), false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(identified, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(anonymous, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        var first = ResultsSqliteStore.Response("r1", identified, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
        first.UserId = "u1";
        first.Number = "R-r1";
        var second = ResultsSqliteStore.Response("r2", anonymous, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["blue"] }));
        second.Number = "R-r2";

        // Even a user id left on an anonymous response is never shown.
        second.UserId = "u2";

        await session.SaveAsync(first, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        await session.SaveAsync(second, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    // Adds completed responses to the anonymous survey, so it reaches its minimum group size of 5.
    private async Task AddAnonymousResponsesAsync(int count)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var anonymous = Version("v2", "survey2");

        await using var session = _store.OpenSession();

        for (var index = 0; index < count; index++)
        {
            var response = ResultsSqliteStore.Response($"a{index}", anonymous, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
            response.Context = new CrestApps.OrchardCore.Records.RecordRef("LearningEnrollment", "e" + index);
            await session.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task GetDataSetsAsync_ForAViewer_ListsTheStaticSetsAndOnePerPublishedSurvey()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var names = (await source.GetDataSetsAsync(new DataSourceContext { User = TestSecurity.User("kim") }, TestContext.Current.CancellationToken)).Select(set => set.Name).ToList();

        // Assert
        Assert.Contains("SurveyResponses", names);
        Assert.Contains("SurveyAnswers", names);
        Assert.Contains("SurveyResponses_csat", names);
        Assert.Contains("SurveyResponses_pulse", names);
    }

    [Fact]
    public async Task GetDataSetsAsync_WithoutAUser_ListsNothing()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var sets = await source.GetDataSetsAsync(new DataSourceContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(sets);
    }

    [Fact]
    public async Task SurveyResponses_ShowsTheRespondentOfIdentifiedSurveys_WithoutExportPermission()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));

        // Assert
        Assert.Equal("Deleted user", rows["r1"]["UserName"]);
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurveyBelowItsMinimumGroupSize_ShowsNoRow()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var answers = await ReadAsync(source, "SurveyAnswers", TestSecurity.User("kim"), "ResponseId");
        var wide = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));

        // Assert
        Assert.Equal(["r1"], rows.Keys);
        Assert.Equal(["r1"], answers.Keys);
        Assert.Empty(wide);
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurveyAtItsMinimumGroupSize_ShowsRowsWithDaysOnlyAndNobody()
    {
        // Arrange
        await AddAnonymousResponsesAsync(4);
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var wide = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));

        // Assert
        Assert.Equal(6, rows.Count);
        Assert.Null(rows["r2"]["UserName"]);
        Assert.Equal("LearningEnrollment", rows["a0"]["ContextKind"]);
        Assert.Null(rows["a0"]["ContextId"]);
        Assert.Equal(_day.Date, Assert.IsType<DateTime>(rows["r2"]["CompletedUtc"]));
        Assert.Equal(_day, Assert.IsType<DateTime>(rows["r1"]["CompletedUtc"]));
        Assert.Equal(5, wide.Count);
        Assert.All(wide.Values, row => Assert.Equal("Anonymous", row["Respondent"]));
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurveyPaddedWithInProgressAndTestResponses_StillShowsNoRow()
    {
        // Arrange: one completed response more, then in-progress, abandoned and test responses well past the minimum of 5.
        var cancellationToken = TestContext.Current.CancellationToken;
        var anonymous = Version("v2", "survey2");

        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(ResultsSqliteStore.Response("c1", anonymous, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);

            for (var index = 0; index < 4; index++)
            {
                await write.SaveAsync(ResultsSqliteStore.Response($"p{index}", anonymous, SurveyResponseStatus.InProgress, _day, ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);
                await write.SaveAsync(ResultsSqliteStore.Response($"x{index}", anonymous, SurveyResponseStatus.Abandoned, _day, ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);

                var test = ResultsSqliteStore.Response($"t{index}", anonymous, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
                test.IsTest = true;
                await write.SaveAsync(test, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            }

            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var wide = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));

        // Assert: two completed, non-test responses never reach the group of 5, whatever else is read with them.
        Assert.Equal(["r1"], rows.Keys);
        Assert.Empty(wide);
    }

    [Fact]
    public async Task WideDataSet_AnAnonymousSurveyFilteredToOnePersonsAnswer_ShowsNoRow()
    {
        // Arrange: 6 completed responses pass the minimum of 5, but only one answered blue.
        await AddAnonymousResponsesAsync(5);
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var blue = new DataCondition { Field = "color", Operator = DataFilterOperator.In, Values = ["blue"] };

        // Act
        var all = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));
        var filtered = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"), conditions: [blue]);

        // Assert: the filter narrows the survey to one person, so the minimum applies after it.
        Assert.Equal(6, all.Count);
        Assert.Empty(filtered);
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurveyAtItsMinimum_LeavesOutAStatusTooFewShare()
    {
        // Arrange: 6 completed responses and one abandoned one.
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddAnonymousResponsesAsync(5);

        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(ResultsSqliteStore.Response("gave-up", Version("v2", "survey2"), SurveyResponseStatus.Abandoned, _day, ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var abandoned = new DataCondition { Field = "Status", Operator = DataFilterOperator.In, Values = [nameof(SurveyResponseStatus.Abandoned)] };

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var filtered = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"), conditions: [abandoned]);
        var wide = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));

        // Assert: the lone abandoned response is left out, read with every status or filtered to.
        Assert.DoesNotContain("gave-up", rows.Keys);
        Assert.Equal(7, rows.Count);
        Assert.DoesNotContain("gave-up", filtered.Keys);
        Assert.DoesNotContain("gave-up", wide.Keys);
        Assert.Equal(6, wide.Count);
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurvey_ShowsNoSiteWideResponseNumber()
    {
        // Arrange
        await AddAnonymousResponsesAsync(4);
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var wide = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));

        // Assert
        Assert.Equal("R-r1", rows["r1"]["Number"]);
        Assert.Equal("r2", rows["r2"]["Number"]);
        Assert.Equal("a0", wide["a0"]["Number"]);
    }

    [Fact]
    public async Task SurveyResponses_WithOnlyOwnSurveys_ReadsOnlyTheirSurveys()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("pat"));

        // Assert
        Assert.Equal(["r1"], rows.Keys);
    }

    [Fact]
    public async Task WideDataSet_HasAFieldPerQuestion()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var schema = await source.GetSchemaAsync("SurveyResponses_csat", new DataSourceContext { User = TestSecurity.User("kim") }, TestContext.Current.CancellationToken);
        var rows = await ReadAsync(source, "SurveyResponses_csat", TestSecurity.User("kim"));

        // Assert
        Assert.NotNull(schema.FindField("color"));
        Assert.Equal("RED", Assert.Single(rows).Value["color"]);
    }

    [Fact]
    public async Task SurveyAnswers_HasOneRowPerAnswerValue_WithItsChoice()
    {
        // Arrange
        await AddAnonymousResponsesAsync(4);
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyAnswers", TestSecurity.User("kim"), "ResponseId");

        // Assert
        Assert.Equal("red", rows["r1"]["ChoiceValue"]);
        Assert.Equal("BLUE", rows["r2"]["ChoiceText"]);
    }

    private static SurveysDataSource Source(ISession session, params Permission[] grants)
    {
        var authorization = TestSecurity.Granting(grants);
        var services = new ServiceCollection().BuildServiceProvider();
        var access = new SurveyDataSetAccess(new SurveyStore(session), authorization, services);
        var registry = Registry();
        var versions = new SurveyVersionStore(session);
        var names = new SurveyRespondentNames(services, RecipeRoundTrip.Localizer<SurveyRespondentNames>());

        ISurveysDataSet[] sets =
        [
            new SurveyResponsesDataSet(session, authorization, access, [], names, versions, new SurveyCollectorStore(session), RecipeRoundTrip.Localizer<SurveyResponsesDataSet>()),
            new SurveyAnswersDataSet(session, authorization, access, new ResultsVersionCache(versions), registry, RecipeRoundTrip.Localizer<SurveyAnswersDataSet>()),
        ];

        return new SurveysDataSource(sets, new SurveyWideDataSetFactory(session, authorization, access, new SurveyStore(session), versions, registry, names, RecipeRoundTrip.Localizer<SurveyWideDataSet>()));
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>>> ReadAsync(SurveysDataSource source, string dataSet, ClaimsPrincipal user, string key = "Id", DataCondition[] conditions = null)
    {
        var query = new DataSourceQuery
        {
            DataSet = dataSet,
            MaxRows = 100,
            Context = new DataSourceContext { User = user },
        };

        foreach (var condition in conditions ?? [])
        {
            query.Conditions.Add(condition);
        }

        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        var fields = table.Fields.Select(field => field.Name).ToList();
        var keyIndex = fields.IndexOf(key);

        return table.Rows.ToDictionary(
            row => (string)row[keyIndex],
            row => (IReadOnlyDictionary<string, object>)fields.Select((name, index) => (name, value: row[index])).ToDictionary(pair => pair.name, pair => pair.value, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    private static SurveyControlRegistry Registry()
    {
        var options = new SurveyControlOptions();
        options.Controls[ResultsTestControlHandler.Name] = new SurveyControlDescriptor
        {
            Name = ResultsTestControlHandler.Name,
            Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices,
        };

        return new SurveyControlRegistry(Options.Create(options), [new ResultsTestControlHandler()], [], []);
    }

    private static Survey Survey(string id, string name, string versionId, string ownerId, AnonymityMode anonymity)
        => new()
        {
            ItemId = id,
            Name = name,
            Title = name.ToUpperInvariant(),
            Status = SurveyStatus.Open,
            PublishedVersionId = versionId,
            PublishedVersionNumber = 1,
            OwnerId = ownerId,
            Access = new SurveyAccessSettings { Anonymity = anonymity },
        };

    private static SurveyVersion Version(string id, string surveyId)
        => ResultsSqliteStore.Version(id, surveyId, 1, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"));
}
