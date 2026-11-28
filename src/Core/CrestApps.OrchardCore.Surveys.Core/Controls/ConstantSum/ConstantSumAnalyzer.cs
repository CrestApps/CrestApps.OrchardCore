using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.ConstantSum;

/// <summary>
/// Summarizes a constant sum question: the average given to each item.
/// </summary>
public sealed class ConstantSumAnalyzer : SurveyAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConstantSumAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public ConstantSumAnalyzer(IStringLocalizer<ConstantSumAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.ConstantSum;

    /// <inheritdoc />
    protected override async Task FillAsync(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context, CancellationToken cancellationToken)
    {
        foreach (var row in question.Rows.Where(row => row is not null))
        {
            var numbers = await context.Aggregates.NumberStatisticsAsync(context.Filter, question.Id, row.Id, cancellationToken);

            if (numbers?.Average is { } average)
            {
                summary.Extra[row.Id] = average;
            }
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<ReportSection> BuildSections(SurveyQuestion question, string title, SurveyQuestionSummary summary, SurveyAnalysisContext context)
    {
        var rows = question.Rows
            .Where(row => row is not null && summary.Extra.ContainsKey(row.Id))
            .Select(row => new ReportRow([row.Text, Format(summary.Extra[row.Id]!.GetValue<decimal>(), context.Culture)]));

        var section = ReportSection.ForTable(title, [new ReportColumn(S["Item"]), new ReportColumn(S["Average"], ReportColumnAlign.End)], rows);
        section.EmptyMessage = S["No answers yet."];

        yield return section;
    }
}
