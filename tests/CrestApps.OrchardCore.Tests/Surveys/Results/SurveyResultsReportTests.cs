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
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// The survey results report: one section per question from its analyzer, and nothing shown for an anonymous survey
/// with fewer responses than its minimum group size.
/// </summary>
public sealed class SurveyResultsReportTests : IAsyncLifetime
{
    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _store = await ResultsSqliteStore.CreateAsync();

        await using var session = _store.OpenSession();
        await SaveSurveyAsync(session, "open", AnonymityMode.Identified, responses: 2);
        await SaveSurveyAsync(session, "anon", AnonymityMode.Anonymous, responses: 2);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task RunAsync_AddsTheSectionsOfEachQuestionsAnalyzer()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var report = Report(session);

        // Act
        var document = await report.RunAsync(Context("open"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("2", document.Sections[0].Metrics[0].Value);
        var section = Assert.Single(document.Sections, section => section.Title == "color");
        Assert.Equal(ReportSectionKind.Bars, section.Kind);
        Assert.Equal(["red: 1", "blue: 1"], section.Bars.Select(bar => $"{bar.Label}: {bar.Value}"));
    }

    [Fact]
    public async Task RunAsync_AnAnonymousSurveyWithTooFewResponses_ShowsNothing()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var report = Report(session);

        // Act
        var document = await report.RunAsync(Context("anon"), TestContext.Current.CancellationToken);

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.Empty(section.Metrics);
        Assert.Contains("anonymous", section.EmptyMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithoutASurvey_AsksToChooseOne()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var report = Report(session);
        var context = new ReportContext(new ReportFilter { ReportName = SurveyReportNames.Results }) { User = TestSecurity.User("kim") };

        // Act
        var document = await report.RunAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(Assert.Single(document.Sections).EmptyMessage);
    }

    private static SurveyResultsReport Report(ISession session)
    {
        var authorization = TestSecurity.Granting(SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var data = new SurveyReportData(
            new SurveyStore(session),
            new SurveyVersionStore(session),
            authorization,
            new ServiceCollection().BuildServiceProvider(),
            new FixedOffsetLocalClock(() => _day, TimeSpan.Zero),
            RecipeRoundTrip.Localizer<SurveyReportData>());
        var options = new SurveyControlOptions();
        options.Controls[ResultsTestControlHandler.Name] = new SurveyControlDescriptor
        {
            Name = ResultsTestControlHandler.Name,
            Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.Aggregatable,
        };

        var registry = new SurveyControlRegistry(Options.Create(options), [new ResultsTestControlHandler()], [], [new CountingAnalyzer()]);

        return new SurveyResultsReport(data, new SurveyResultsQueries(session), new SurveyAnswerAggregates(session), registry, RecipeRoundTrip.Localizer<SurveyResultsReport>());
    }

    private static ReportContext Context(string surveyId)
    {
        var filter = new ReportFilter { ReportName = SurveyReportNames.Results };
        filter.Set(SurveyReportKeys.SurveyId, surveyId);

        return new ReportContext(filter) { User = TestSecurity.User("kim") };
    }

    private static async Task SaveSurveyAsync(ISession session, string id, AnonymityMode anonymity, int responses)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var version = ResultsSqliteStore.Version(id + "-v1", id, 1, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"));

        await session.SaveAsync(new Survey
        {
            ItemId = id,
            Name = id,
            Title = id,
            Status = SurveyStatus.Open,
            PublishedVersionId = version.ItemId,
            PublishedVersionNumber = 1,
            Access = new SurveyAccessSettings { Anonymity = anonymity, MinimumGroupSize = 5 },
        }, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(version, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        for (var index = 0; index < responses; index++)
        {
            var response = ResultsSqliteStore.Response($"{id}-r{index}", version, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = [index % 2 == 0 ? "red" : "blue"] }));
            await session.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        }
    }

    private sealed class CountingAnalyzer : ISurveyControlAnalyzer
    {
        public string ControlType => ResultsTestControlHandler.Name;

        public async Task<SurveyQuestionSummary> SummarizeAsync(SurveyQuestion question, SurveyAnalysisContext context, CancellationToken cancellationToken)
        {
            var counts = await context.Aggregates.CountChoicesAsync(context.Filter, question.Id, cancellationToken);

            return new SurveyQuestionSummary
            {
                QuestionId = question.Id,
                ChoiceCounts = counts.GetValueOrDefault(string.Empty)?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [],
            };
        }

        public IEnumerable<ReportSection> ToReportSections(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context)
        {
            yield return ReportSection.ForBars(question.Name, question.Choices.Select(choice => new ReportBar(choice.Id, summary.ChoiceCounts.GetValueOrDefault(choice.Id).ToString(System.Globalization.CultureInfo.InvariantCulture), 0)));
        }
    }
}
