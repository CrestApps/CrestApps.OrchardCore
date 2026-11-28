using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;
using CrestApps.OrchardCore.Surveys.Core.Controls.Rating;
using CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using static CrestApps.OrchardCore.Tests.Surveys.TestSupport.ControlsTestData;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

/// <summary>
/// The analyzers turn the aggregates of the answer index into report sections.
/// </summary>
public sealed class SurveyControlAnalyzerTests
{
    [Fact]
    public async Task SingleChoice_Summarize_CountsEachChoiceAsBars()
    {
        var question = Question(SurveyControlNames.SingleChoice, ("a", "Apple"), ("b", "Banana"));
        var aggregates = new ControlsFakeAggregates(10, 8, new() { [string.Empty] = new() { ["a"] = 6, ["b"] = 2 } });
        var analyzer = new SingleChoiceAnalyzer(new ControlsTestLocalizer<SingleChoiceAnalyzer>());
        var context = new SurveyAnalysisContext { Aggregates = aggregates };

        var summary = await analyzer.SummarizeAsync(question, context, CancellationToken.None);
        var section = analyzer.ToReportSections(question, summary, context).Single();

        Assert.Equal(ReportSectionKind.Bars, section.Kind);
        Assert.Equal(["Apple", "Banana"], section.Bars.Select(bar => bar.Label));
        Assert.Equal(0.75, section.Bars[0].Ratio);
    }

    [Fact]
    public async Task Summarize_BelowTheMinimumGroupSize_ShowsNothing()
    {
        var question = Question(SurveyControlNames.SingleChoice, ("a", "Apple"));
        var analyzer = new SingleChoiceAnalyzer(new ControlsTestLocalizer<SingleChoiceAnalyzer>());
        var context = new SurveyAnalysisContext { Aggregates = new ControlsFakeAggregates(3, 3, []), MinimumGroupSize = 5 };

        var summary = await analyzer.SummarizeAsync(question, context, CancellationToken.None);
        var section = analyzer.ToReportSections(question, summary, context).Single();

        Assert.Empty(summary.ChoiceCounts);
        Assert.Empty(section.Bars);
        Assert.NotNull(section.EmptyMessage);
    }

    [Fact]
    public void Nps_ComputesPromotersLessDetractors()
    {
        var distribution = new Dictionary<decimal, long> { [10] = 5, [9] = 1, [8] = 2, [3] = 2 };

        var nps = RatingAnalyzer.Nps(distribution);

        Assert.Equal(6, nps.Value.Promoters);
        Assert.Equal(2, nps.Value.Passives);
        Assert.Equal(2, nps.Value.Detractors);
        Assert.Equal(40m, nps.Value.Score);
    }

    [Fact]
    public void Csat_ComputesTheShareOfTheTwoHighestValues()
    {
        var distribution = new Dictionary<decimal, long> { [5] = 3, [4] = 1, [2] = 4 };

        Assert.Equal(50m, RatingAnalyzer.Csat(distribution, 5));
    }

    [Fact]
    public async Task Rating_ToReportSections_AddsTheNetPromoterScore()
    {
        var question = Question(SurveyControlNames.Rating);
        question.SetSettings(new RatingSettings { Min = 0, Max = 10, Metric = RatingMetric.Nps });
        var numbers = new SurveyNumberStatistics { Count = 4, Average = 8, Distribution = new() { [10] = 3, [2] = 1 } };
        var analyzer = new RatingAnalyzer(new ControlsTestLocalizer<RatingAnalyzer>());
        var context = new SurveyAnalysisContext { Aggregates = new ControlsFakeAggregates(4, 4, [], numbers) };

        var summary = await analyzer.SummarizeAsync(question, context, CancellationToken.None);
        var sections = analyzer.ToReportSections(question, summary, context).ToList();

        Assert.Contains(sections, section => section.Title == "Net Promoter Score" && section.Metrics[0].Value == "50");
    }

    [Fact]
    public async Task CarriedForward_Summarize_LabelsTheBarsWithTheSourceChoiceTexts()
    {
        // Round 12 M8: a question that carries its choices forward has none of its own.
        var source = Question(SurveyControlNames.SingleChoice, ("a", "Apple"), ("b", "Banana"));
        var carried = new SurveyQuestion { Id = "q2", Name = "q_two", ControlType = SurveyControlNames.SingleChoice, ChoicesFrom = source.Id };
        var definition = new SurveyDefinition { Pages = [new SurveyPage { Id = "p1", Questions = [source, carried] }] };
        var aggregates = new ControlsFakeAggregates(4, 4, new() { [string.Empty] = new() { ["a"] = 3, ["b"] = 1 } });
        var analyzer = new SingleChoiceAnalyzer(new ControlsTestLocalizer<SingleChoiceAnalyzer>());
        var context = new SurveyAnalysisContext { Aggregates = aggregates, Format = new SurveyFormatContext { Definition = definition } };

        var summary = await analyzer.SummarizeAsync(carried, context, CancellationToken.None);
        var section = analyzer.ToReportSections(carried, summary, context).Single();

        Assert.Equal(["Apple", "Banana"], section.Bars.Select(bar => bar.Label));
    }

    [Fact]
    public async Task CarriedForward_Ranking_Summarize_ListsTheSourceItems()
    {
        // Round 12 M8: a ranking that carries its items forward has none of its own.
        var source = Question(SurveyControlNames.MultipleChoice, ("a", "Apple"), ("b", "Banana"));
        var carried = new SurveyQuestion { Id = "q2", Name = "q_two", ControlType = SurveyControlNames.Ranking, ChoicesFrom = source.Id };
        var definition = new SurveyDefinition { Pages = [new SurveyPage { Id = "p1", Questions = [source, carried] }] };
        var numbers = new SurveyNumberStatistics { Count = 4, Average = 1.5m };
        var analyzer = new RankingAnalyzer(new ControlsTestLocalizer<RankingAnalyzer>());
        var context = new SurveyAnalysisContext { Aggregates = new ControlsFakeAggregates(4, 4, [], numbers), Format = new SurveyFormatContext { Definition = definition } };

        var summary = await analyzer.SummarizeAsync(carried, context, CancellationToken.None);
        var section = analyzer.ToReportSections(carried, summary, context).Single();

        Assert.Equal(2, section.Rows.Count);
    }

    private sealed class ControlsFakeAggregates : ISurveyAnswerAggregates
    {
        private readonly long _responses;
        private readonly long _answered;
        private readonly Dictionary<string, Dictionary<string, long>> _choices;
        private readonly SurveyNumberStatistics _numbers;

        public ControlsFakeAggregates(long responses, long answered, Dictionary<string, Dictionary<string, long>> choices, SurveyNumberStatistics numbers = null)
        {
            _responses = responses;
            _answered = answered;
            _choices = choices;
            _numbers = numbers ?? new SurveyNumberStatistics();
        }

        public Task<long> CountResponsesAsync(SurveyAnswerFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult(_responses);

        public Task<long> CountAnsweredAsync(SurveyAnswerFilter filter, string questionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_answered);

        public Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, long>>> CountChoicesAsync(SurveyAnswerFilter filter, string questionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyDictionary<string, long>>>(_choices.ToDictionary(entry => entry.Key, entry => (IReadOnlyDictionary<string, long>)entry.Value));

        public Task<SurveyNumberStatistics> NumberStatisticsAsync(SurveyAnswerFilter filter, string questionId, string rowId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_numbers);

        public Task<IReadOnlyList<string>> TextSamplesAsync(SurveyAnswerFilter filter, string questionId, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyCollection<string>> ResponsesWithChoiceAsync(SurveyAnswerFilter filter, string questionId, string choiceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<string>>([]);
    }
}
