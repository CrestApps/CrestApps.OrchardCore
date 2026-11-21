using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// Summarizes a text question: how many answered and a sample of the latest answers.
/// </summary>
public abstract class SurveyTextAnalyzerBase : SurveyAnalyzerBase
{
    private const int SampleSize = 20;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTextAnalyzerBase"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    protected SurveyTextAnalyzerBase(IStringLocalizer stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override async Task FillAsync(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context, CancellationToken cancellationToken)
        => summary.TextSamples = (await context.Aggregates.TextSamplesAsync(context.Filter, question.Id, SampleSize, cancellationToken)).ToList();

    /// <inheritdoc />
    protected override IEnumerable<ReportSection> BuildSections(SurveyQuestion question, string title, SurveyQuestionSummary summary, SurveyAnalysisContext context)
    {
        var section = ReportSection.ForTable(
            title,
            [new ReportColumn(S["Latest answers"])],
            summary.TextSamples.Select(text => new ReportRow([text])));

        section.Description = S["{0} of {1} responses answered.", summary.Answered, summary.Responses];
        section.EmptyMessage = S["No answers yet."];

        yield return section;
    }
}
