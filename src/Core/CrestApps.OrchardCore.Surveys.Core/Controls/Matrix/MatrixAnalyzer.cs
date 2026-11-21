using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Matrix;

/// <summary>
/// Summarizes a matrix: a table of rows by columns with the count of each cell.
/// </summary>
public sealed class MatrixAnalyzer : SurveyAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MatrixAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MatrixAnalyzer(IStringLocalizer<MatrixAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Matrix;

    /// <inheritdoc />
    protected override async Task FillAsync(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context, CancellationToken cancellationToken)
    {
        var counts = await context.Aggregates.CountChoicesAsync(context.Filter, question.Id, cancellationToken);

        foreach (var (rowId, row) in counts)
        {
            summary.RowChoiceCounts[rowId] = row.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<ReportSection> BuildSections(SurveyQuestion question, string title, SurveyQuestionSummary summary, SurveyAnalysisContext context)
    {
        var columnChoices = question.Choices.Where(choice => choice is not null).ToList();
        var columns = new List<ReportColumn> { new(S["Row"]) };

        columns.AddRange(columnChoices.Select(choice => new ReportColumn(choice.Text, ReportColumnAlign.End)));

        var rows = new List<ReportRow>();

        foreach (var row in question.Rows.Where(row => row is not null))
        {
            var counts = summary.RowChoiceCounts.GetValueOrDefault(row.Id) ?? [];
            var total = counts.Values.Sum();
            var cells = new List<string> { row.Text };

            cells.AddRange(columnChoices.Select(choice =>
            {
                var count = counts.GetValueOrDefault(choice.Id);

                return total > 0 ? $"{ReportFormat.Number(count)} ({ReportFormat.Percent((double)count / total)})" : "0";
            }));

            rows.Add(new ReportRow(cells));
        }

        var section = ReportSection.ForTable(title, columns, rows);
        section.Description = S["{0} of {1} responses answered.", summary.Answered, summary.Responses];

        yield return section;
    }
}
