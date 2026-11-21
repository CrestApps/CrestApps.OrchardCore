using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// Summarizes a choice question: how many times each choice was chosen, as bars.
/// </summary>
public abstract class SurveyChoiceAnalyzerBase : SurveyAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyChoiceAnalyzerBase"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    protected SurveyChoiceAnalyzerBase(IStringLocalizer stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <summary>
    /// Gets the options of a question in display order, by id and label.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="context">The analysis context.</param>
    /// <returns>The options.</returns>
    protected virtual IEnumerable<(string Id, string Label)> GetOptions(SurveyQuestion question, SurveyAnalysisContext context)
        => (question.Choices ?? [])
            .Where(choice => choice is not null)
            .Select(choice => (choice.Id, context.Format?.Text($"choice:{question.Id}:{choice.Id}", choice.Text) ?? choice.Text));

    /// <inheritdoc />
    protected override async Task FillAsync(SurveyQuestion question, SurveyQuestionSummary summary, SurveyAnalysisContext context, CancellationToken cancellationToken)
    {
        var counts = await context.Aggregates.CountChoicesAsync(context.Filter, question.Id, cancellationToken);

        foreach (var row in counts.Values)
        {
            foreach (var (choiceId, count) in row)
            {
                summary.ChoiceCounts[choiceId] = summary.ChoiceCounts.GetValueOrDefault(choiceId) + count;
            }
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<ReportSection> BuildSections(SurveyQuestion question, string title, SurveyQuestionSummary summary, SurveyAnalysisContext context)
    {
        var bars = new List<ReportBar>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (id, label) in GetOptions(question, context))
        {
            seen.Add(id);
            bars.Add(Bar(label, summary.ChoiceCounts.GetValueOrDefault(id), summary.Answered));
        }

        foreach (var (id, count) in summary.ChoiceCounts.Where(entry => !seen.Contains(entry.Key)))
        {
            bars.Add(Bar(id, count, summary.Answered));
        }

        var section = ReportSection.ForBars(title, bars);
        section.Description = S["{0} of {1} responses answered.", summary.Answered, summary.Responses];
        section.EmptyMessage = S["No answers yet."];

        yield return section;
    }
}
