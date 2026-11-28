using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;

/// <summary>
/// Summarizes a ranking: the average rank of each item, best first.
/// </summary>
public sealed class RankingAnalyzer : SurveyAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RankingAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public RankingAnalyzer(IStringLocalizer<RankingAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Ranking;

    /// <inheritdoc />
    protected override async Task FillAsync(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context, CancellationToken cancellationToken)
    {
        foreach (var choice in SurveyChoiceSource.GetChoices(question, context.Format?.Definition).Where(choice => choice is not null))
        {
            var numbers = await context.Aggregates.NumberStatisticsAsync(context.Filter, question.Id, choice.Id, cancellationToken);

            if (numbers?.Average is { } average)
            {
                summary.Extra[choice.Id] = average;
                summary.ChoiceCounts[choice.Id] = numbers.Count;
            }
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<ReportSection> BuildSections(SurveyQuestion question, string title, SurveyQuestionSummary summary, SurveyAnalysisContext context)
    {
        var rows = SurveyChoiceSource.GetChoices(question, context.Format?.Definition)
            .Where(choice => choice is not null && summary.Extra.ContainsKey(choice.Id))
            .Select(choice => (choice, Average: summary.Extra[choice.Id]!.GetValue<decimal>()))
            .OrderBy(entry => entry.Average)
            .Select(entry => new ReportRow([entry.choice.Text, Format(entry.Average, context.Culture), ReportFormat.Number(summary.ChoiceCounts.GetValueOrDefault(entry.choice.Id))]));

        var section = ReportSection.ForTable(title, [new ReportColumn(S["Item"]), new ReportColumn(S["Average rank"], ReportColumnAlign.End), new ReportColumn(S["Ranked by"], ReportColumnAlign.End)], rows);
        section.EmptyMessage = S["No answers yet."];

        yield return section;
    }
}
