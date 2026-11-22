using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Reports;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// The Survey Responses report: the minimum group size of anonymous surveys, the respondent names, the status filter,
/// days-only times on anonymous surveys and answer dates that are never shifted.
/// </summary>
public sealed class SurveyResponsesReportTests : IAsyncLifetime
{
    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
        => _store = await ResultsSqliteStore.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task RunAsync_AnAnonymousSurveyBelowItsMinimumGroupSize_ShowsNoRow()
    {
        // Arrange
        await SaveSurveyAsync("anon", AnonymityMode.Anonymous, completed: 4);
        await using var session = _store.OpenSession();

        // Act
        var document = await Report(session).RunAsync(Context("anon"), TestContext.Current.CancellationToken);

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.Empty(section.Rows);
        Assert.Contains("anonymous", section.EmptyMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_AnAnonymousSurveyPaddedByInProgressAndTestResponses_StillShowsNoRow()
    {
        // Arrange: 4 real completed responses, below the minimum of 5, padded by unfinished and test responses that the
        // filters (every status, tests included) would list.
        await SaveSurveyAsync("anon", AnonymityMode.Anonymous, completed: 4, inProgress: 3);
        await SaveTestResponsesAsync("anon", 3);
        await using var session = _store.OpenSession();
        var context = Context("anon");
        context.Filter.Set(SurveyReportKeys.Status, SurveyReportKeys.AllStatuses);
        context.Filter.Set(SurveyReportKeys.IncludeTest, true);

        // Act
        var document = await Report(session).RunAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.Empty(section.Rows);
        Assert.Contains("anonymous", section.EmptyMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_AnAnonymousSurveyAtItsMinimum_ListsEveryFilteredRow()
    {
        // Arrange: 5 real completed responses reach the minimum, so the in-progress rows the filter asks for show too.
        await SaveSurveyAsync("anon", AnonymityMode.Anonymous, completed: 5, inProgress: 2);
        await using var session = _store.OpenSession();
        var context = Context("anon");
        context.Filter.Set(SurveyReportKeys.Status, SurveyReportKeys.AllStatuses);

        // Act
        var document = await Report(session).RunAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(7, Assert.Single(document.Sections).Rows.Count);
    }

    [Fact]
    public async Task RunAsync_AnAnonymousSurveyWithAMinimumOfZero_StillHonorsTheSiteFloor()
    {
        // Arrange
        await SaveSurveyAsync("anon", AnonymityMode.Anonymous, completed: 2, minimumGroupSize: 0);
        await using var session = _store.OpenSession();

        // Act
        var document = await Report(session).RunAsync(Context("anon"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Assert.Single(document.Sections).Rows);
    }

    [Fact]
    public async Task RunAsync_AnAnonymousSurveyAtItsMinimumGroupSize_NamesNobodyAndShowsDaysOnly()
    {
        // Arrange
        await SaveSurveyAsync("anon", AnonymityMode.Anonymous, completed: 5);
        await using var session = _store.OpenSession();

        // Act
        var document = await Report(session).RunAsync(Context("anon"), TestContext.Current.CancellationToken);

        // Assert
        var section = Assert.Single(document.Sections);
        var respondent = section.Columns.Select(column => column.Label).ToList().IndexOf("Respondent");
        var completed = section.Columns.Select(column => column.Label).ToList().IndexOf("Completed");
        Assert.Equal(5, section.Rows.Count);
        Assert.All(section.Rows, row => Assert.Equal("Anonymous", row.Cells[respondent]));
        Assert.All(section.Rows, row => Assert.Equal(_day.ToString("d", CultureInfo.CurrentCulture), row.Cells[completed]));
    }

    [Fact]
    public async Task RunAsync_ByDefault_ListsCompletedResponsesOnly_AndNamesGuestsAndDeletedUsers()
    {
        // Arrange
        await SaveSurveyAsync("open", AnonymityMode.Identified, completed: 2, inProgress: 1, userIds: [null, "gone-user"]);
        await using var session = _store.OpenSession();

        // Act
        var document = await Report(session).RunAsync(Context("open"), TestContext.Current.CancellationToken);

        // Assert
        var section = Assert.Single(document.Sections);
        var respondent = section.Columns.Select(column => column.Label).ToList().IndexOf("Respondent");
        Assert.Equal(2, section.Rows.Count);
        Assert.Equal(["Deleted user", "Guest"], section.Rows.Select(row => row.Cells[respondent]).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WithEveryStatus_ListsResponsesInProgressToo()
    {
        // Arrange
        await SaveSurveyAsync("open", AnonymityMode.Identified, completed: 2, inProgress: 1);
        await using var session = _store.OpenSession();
        var context = Context("open");
        context.Filter.Set(SurveyReportKeys.Status, SurveyReportKeys.AllStatuses);

        // Act
        var document = await Report(session).RunAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, Assert.Single(document.Sections).Rows.Count);
    }

    [Theory]
    [InlineData(DataFieldType.Date)]
    [InlineData(DataFieldType.DateTime)]
    public void FormatAnswerCell_ADateAnswer_IsShownAsTypedWithoutAShift(DataFieldType type)
    {
        // Arrange: a date the respondent typed is a wall-clock value, stored without a time zone.
        var answered = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        var text = SurveyReportCells.Format(answered, type, CultureInfo.InvariantCulture, "Yes", "No");

        // Assert
        Assert.StartsWith("03/01/2026", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatAnswerCell_NoAnswer_IsBlankNotNo()
    {
        // Act
        var text = SurveyReportCells.Format(null, DataFieldType.Boolean, CultureInfo.InvariantCulture, "Yes", "No");

        // Assert
        Assert.Equal(string.Empty, text);
    }

    private static SurveyResponsesReport Report(ISession session)
    {
        var authorization = TestSecurity.Granting(SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var services = new ServiceCollection().AddSingleton(authorization).BuildServiceProvider();
        var names = new SurveyRespondentNames(services, RecipeRoundTrip.Localizer<SurveyRespondentNames>());

        // The clock is ten hours behind UTC, so a time shifted as if it were UTC would land on another day.
        var data = new SurveyReportData(
            new SurveyStore(session),
            new SurveyVersionStore(session),
            authorization,
            services,
            new FixedOffsetLocalClock(() => _day, TimeSpan.FromHours(-10)),
            names,
            RecipeRoundTrip.Localizer<SurveyReportData>());
        var options = new SurveyControlOptions();
        options.Controls[ResultsTestControlHandler.Name] = new SurveyControlDescriptor
        {
            Name = ResultsTestControlHandler.Name,
            Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices,
        };

        var registry = new SurveyControlRegistry(Options.Create(options), [new ResultsTestControlHandler()], [], []);

        return new SurveyResponsesReport(data, new SurveyResultsQueries(session), registry, names, RecipeRoundTrip.Localizer<SurveyResponsesReport>());
    }

    private static ReportContext Context(string surveyId)
    {
        var filter = new ReportFilter { ReportName = SurveyReportNames.Responses };
        filter.Set(SurveyReportKeys.SurveyId, surveyId);

        return new ReportContext(filter) { User = TestSecurity.User("kim") };
    }

    private async Task SaveSurveyAsync(string id, AnonymityMode anonymity, int completed, int inProgress = 0, int minimumGroupSize = 5, string[] userIds = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var version = ResultsSqliteStore.Version(id + "-v1", id, 1, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"));

        await using var session = _store.OpenSession();
        await session.SaveAsync(new Survey
        {
            ItemId = id,
            Name = id,
            Title = id,
            Status = SurveyStatus.Open,
            PublishedVersionId = version.ItemId,
            PublishedVersionNumber = 1,
            Access = new SurveyAccessSettings { Anonymity = anonymity, MinimumGroupSize = minimumGroupSize },
        }, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(version, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        for (var index = 0; index < completed + inProgress; index++)
        {
            var status = index < completed ? SurveyResponseStatus.Completed : SurveyResponseStatus.InProgress;
            var response = ResultsSqliteStore.Response($"{id}-r{index}", version, status, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
            response.UserId = userIds is not null && index < userIds.Length ? userIds[index] : null;
            await session.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveTestResponsesAsync(string id, int count)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var version = ResultsSqliteStore.Version(id + "-v1", id, 1, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"));

        await using var session = _store.OpenSession();

        for (var index = 0; index < count; index++)
        {
            var response = ResultsSqliteStore.Response($"{id}-t{index}", version, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
            response.IsTest = true;
            await session.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}
