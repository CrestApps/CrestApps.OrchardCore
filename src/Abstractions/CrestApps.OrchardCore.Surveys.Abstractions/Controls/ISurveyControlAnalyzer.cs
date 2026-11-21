using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// Summarizes the answers of one question type for results. Register an implementation with
/// <c>services.AddSurveyControlAnalyzer&lt;TAnalyzer&gt;()</c>.
/// </summary>
public interface ISurveyControlAnalyzer
{
    /// <summary>
    /// Gets the name of the question type the analyzer serves.
    /// </summary>
    string ControlType { get; }

    /// <summary>
    /// Summarizes the answers of a question, with aggregates computed in SQL over the answer index.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="context">The analysis context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The summary.</returns>
    Task<SurveyQuestionSummary> SummarizeAsync(SurveyQuestion question, SurveyAnalysisContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Turns a summary into report sections.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="context">The analysis context.</param>
    /// <returns>The sections.</returns>
    IEnumerable<ReportSection> ToReportSections(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context);
}
