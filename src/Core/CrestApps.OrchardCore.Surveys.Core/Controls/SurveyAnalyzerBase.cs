using System.Globalization;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// The base of the built-in analyzers: counts, the minimum group size of anonymous surveys and section helpers.
/// </summary>
public abstract class SurveyAnalyzerBase : ISurveyControlAnalyzer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnalyzerBase"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    protected SurveyAnalyzerBase(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public abstract string ControlType { get; }

    /// <inheritdoc />
    public async Task<SurveyQuestionSummary> SummarizeAsync(SurveyQuestion question, SurveyAnalysisContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(context);

        var summary = new SurveyQuestionSummary { QuestionId = question.Id };

        if (context.Aggregates is null)
        {
            return summary;
        }

        summary.Responses = await context.Aggregates.CountResponsesAsync(context.Filter, cancellationToken);
        summary.Answered = await context.Aggregates.CountAnsweredAsync(context.Filter, question.Id, cancellationToken);

        if (!IsTooSmall(summary, context))
        {
            await FillAsync(question, summary, context, cancellationToken);
        }

        return summary;
    }

    /// <inheritdoc />
    public IEnumerable<ReportSection> ToReportSections(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(context);

        var title = string.IsNullOrWhiteSpace(question.Title) ? question.Name : question.Title;

        if (IsTooSmall(summary, context))
        {
            return
            [
                new ReportSection
                {
                    Title = title,
                    Kind = ReportSectionKind.Metrics,
                    EmptyMessage = S["Too few responses to show this question without identifying respondents."],
                },
            ];
        }

        return BuildSections(question, title, summary, context);
    }

    /// <summary>
    /// Adds the aggregates of the control to the summary.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="summary">The summary with the counts.</param>
    /// <param name="context">The analysis context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    protected abstract Task FillAsync(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Builds the report sections of a summary.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="title">The section title.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="context">The analysis context.</param>
    /// <returns>The sections.</returns>
    protected abstract IEnumerable<ReportSection> BuildSections(SurveyQuestion question, string title, SurveyQuestionSummary summary, SurveyAnalysisContext context);

    /// <summary>
    /// Builds a bar for a count out of a total.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="count">The count.</param>
    /// <param name="total">The total.</param>
    /// <returns>The bar.</returns>
    protected static ReportBar Bar(string label, long count, long total)
    {
        var ratio = total > 0 ? (double)count / total : 0;

        return new ReportBar(label, $"{ReportFormat.Number(count)} ({ReportFormat.Percent(ratio)})", ratio);
    }

    /// <summary>
    /// Formats a number for a report.
    /// </summary>
    /// <param name="value">The number, or <see langword="null"/>.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The text.</returns>
    protected static string Format(decimal? value, CultureInfo culture)
        => value is { } number ? Math.Round(number, 2, MidpointRounding.AwayFromZero).ToString("0.##", culture) : "-";

    /// <summary>
    /// Builds the answered metric.
    /// </summary>
    /// <param name="summary">The summary.</param>
    /// <returns>The metric.</returns>
    protected ReportMetric AnsweredMetric(SurveyQuestionSummary summary)
        => new(S["Answered"], ReportFormat.Number(summary.Answered), S["of {0} responses", ReportFormat.Number(summary.Responses)]);

    private static bool IsTooSmall(SurveyQuestionSummary summary, SurveyAnalysisContext context)
        => context.MinimumGroupSize > 0 && summary.Responses < context.MinimumGroupSize;
}
