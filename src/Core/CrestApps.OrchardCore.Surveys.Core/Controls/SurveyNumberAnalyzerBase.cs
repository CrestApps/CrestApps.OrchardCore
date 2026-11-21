using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// Summarizes a number question: average, smallest, largest and median, and the distribution of values.
/// </summary>
public abstract class SurveyNumberAnalyzerBase : SurveyAnalyzerBase
{
    private const int MaxDistributionBars = 25;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyNumberAnalyzerBase"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    protected SurveyNumberAnalyzerBase(IStringLocalizer stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override async Task FillAsync(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context, CancellationToken cancellationToken)
        => summary.Numbers = await context.Aggregates.NumberStatisticsAsync(context.Filter, question.Id, null, cancellationToken);

    /// <inheritdoc />
    protected override IEnumerable<ReportSection> BuildSections(SurveyQuestion question, string title, SurveyQuestionSummary summary, SurveyAnalysisContext context)
    {
        var numbers = summary.Numbers ?? new SurveyNumberStatistics();
        var culture = context.Culture;

        yield return ReportSection.ForMetrics(title,
        [
            AnsweredMetric(summary),
            new ReportMetric(S["Average"], Format(numbers.Average, culture)),
            new ReportMetric(S["Median"], Format(numbers.Median, culture)),
            new ReportMetric(S["Lowest"], Format(numbers.Min, culture)),
            new ReportMetric(S["Highest"], Format(numbers.Max, culture)),
        ]);

        foreach (var section in ExtraSections(question, title, summary, context))
        {
            yield return section;
        }

        if (numbers.Distribution.Count is > 0 and <= MaxDistributionBars)
        {
            var total = numbers.Distribution.Values.Sum();
            var bars = numbers.Distribution
                .OrderBy(entry => entry.Key)
                .Select(entry => Bar(Format(entry.Key, culture), entry.Value, total));

            yield return ReportSection.ForBars(S["Distribution of {0}", title], bars);
        }
    }

    /// <summary>
    /// Gets sections shown between the metrics and the distribution, such as a Net Promoter Score.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="title">The title.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="context">The analysis context.</param>
    /// <returns>The sections.</returns>
    protected virtual IEnumerable<ReportSection> ExtraSections(SurveyQuestion question, string title, SurveyQuestionSummary summary, SurveyAnalysisContext context)
        => [];
}
