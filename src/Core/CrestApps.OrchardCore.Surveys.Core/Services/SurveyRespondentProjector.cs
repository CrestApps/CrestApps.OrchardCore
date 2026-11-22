using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Builds what respondents may see of a question. It copies texts, choices and rows and the settings the handler allows,
/// and never the scoring key.
/// </summary>
public sealed class SurveyRespondentProjector
{
    private readonly ISurveyControlRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyRespondentProjector"/> class.
    /// </summary>
    /// <param name="registry">The control registry.</param>
    public SurveyRespondentProjector(ISurveyControlRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Builds the respondent view of a question.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="definition">The definition, for carried-forward choices.</param>
    /// <param name="plan">The response plan, for choice order, or <see langword="null"/>.</param>
    /// <param name="hiddenChoiceIds">The ids of choices hidden now, or <see langword="null"/>.</param>
    /// <param name="pipe">Pipes and translates a text: the text key, then the text. <see langword="null"/> keeps the text.</param>
    /// <returns>The view.</returns>
    public SurveyQuestionView Project(
        SurveyQuestion question,
        SurveyDefinition definition,
        SurveyResponsePlan plan,
        IReadOnlyCollection<string> hiddenChoiceIds,
        Func<string, string, string> pipe)
    {
        ArgumentNullException.ThrowIfNull(question);

        pipe ??= (_, text) => text;

        var handler = _registry.GetHandler(question.ControlType);
        var choices = SurveyChoiceSource.GetChoices(question, definition);
        var order = plan?.ChoiceOrder is not null && plan.ChoiceOrder.TryGetValue(question.Id, out var planned)
            ? planned
            : SurveyChoiceSource.OrderChoices(choices, shuffle: false, random: null);
        var byId = choices.Where(choice => !string.IsNullOrEmpty(choice?.Id)).GroupBy(choice => choice.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var view = new SurveyQuestionView
        {
            Id = question.Id,
            Name = question.Name,
            ControlType = question.ControlType,
            Title = pipe($"title:{question.Id}", question.Title),
            Description = pipe($"description:{question.Id}", question.Description),
            Placeholder = pipe($"placeholder:{question.Id}", question.Placeholder),
            Required = question.Required,
            Settings = handler?.GetRespondentSettings(question) ?? [],
        };

        // A choice's export value is the author's code, and codes often follow the key (a ranking numbered in its right
        // order, a choice coded "correct"), so a question that is scored never sends them. Respondents answer by id.
        var sendValues = definition?.Scoring?.Enabled != true
            || _registry.GetControl(question.ControlType)?.Has(SurveyControlCapabilities.Scorable) != true;

        foreach (var id in order)
        {
            if (!byId.TryGetValue(id, out var choice) || choice.Retired || (hiddenChoiceIds?.Contains(id) ?? false))
            {
                continue;
            }

            view.Choices.Add(new SurveyChoiceView
            {
                Id = choice.Id,
                Value = sendValues ? choice.Value : null,
                Text = pipe($"choice:{question.Id}:{choice.Id}", choice.Text),
                ImageUrl = choice.ImageUrl,
                IsOther = choice.IsOther,
                IsExclusive = choice.IsExclusive,
            });
        }

        foreach (var row in question.Rows ?? [])
        {
            if (row is null || row.Retired)
            {
                continue;
            }

            view.Rows.Add(new SurveySubQuestionView
            {
                Id = row.Id,
                Name = row.Name,
                Text = pipe($"row:{question.Id}:{row.Id}", row.Text),
            });
        }

        return view;
    }
}
