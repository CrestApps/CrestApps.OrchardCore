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
        var second = ResultsSqliteStore.Response("r2", anonymous, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["blue"] }));

        // Even a user id left on an anonymous response is never shown.
        second.UserId = "u2";

        await session.SaveAsync(first, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        await session.SaveAsync(second, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
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

    [Theory]
    [InlineData(true, "u1")]
    [InlineData(false, null)]
    public async Task SurveyResponses_ShowsTheRespondentOnlyWithExport_AndNeverForAnonymousSurveys(bool canExport, string expectedUser)
    {
        // Arrange
        await using var session = _store.OpenSession();
        Permission[] grants = canExport
            ? [SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses, SurveysPermissions.ExportSurveyResponses]
            : [SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses];

        // Act
        var rows = await ReadAsync(Source(session, grants), "SurveyResponses", TestSecurity.User("kim"));

        // Assert
        Assert.Equal(expectedUser, rows["r1"]["UserName"]);
        Assert.Null(rows["r2"]["UserName"]);
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

        ISurveysDataSet[] sets =
        [
            new SurveyResponsesDataSet(session, authorization, access, [], new SurveyRespondentNames(services), versions, new SurveyCollectorStore(session), RecipeRoundTrip.Localizer<SurveyResponsesDataSet>()),
            new SurveyAnswersDataSet(session, authorization, access, new ResultsVersionCache(versions), registry, RecipeRoundTrip.Localizer<SurveyAnswersDataSet>()),
        ];

        return new SurveysDataSource(sets, new SurveyWideDataSetFactory(session, authorization, access, new SurveyStore(session), versions, registry, RecipeRoundTrip.Localizer<SurveyWideDataSet>()));
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>>> ReadAsync(SurveysDataSource source, string dataSet, ClaimsPrincipal user, string key = "Id")
    {
        var table = await ReportDesignerTestServices.ReadAsync(source, new DataSourceQuery
        {
            DataSet = dataSet,
            MaxRows = 100,
            Context = new DataSourceContext { User = user },
        }, TestContext.Current.CancellationToken);

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
