using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Builds the list of sources a rule may read, for the rule builder: earlier questions with their choices and rows,
/// variables and scores.
/// </summary>
internal static class SurveyRuleSources
{
    /// <summary>
    /// Builds the sources as JSON.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="registry">The control registry.</param>
    /// <param name="lastPageIndex">The last page whose questions may be read.</param>
    /// <param name="excludeQuestionId">A question that may not be read (the question itself), or <see langword="null"/>.</param>
    /// <param name="excludeLastPage">Whether the questions of the last page are left out (page rules).</param>
    /// <returns>The JSON.</returns>
    public static string Build(SurveyDefinition definition, ISurveyControlRegistry registry, int lastPageIndex, string excludeQuestionId = null, bool excludeLastPage = false)
    {
        var sources = new List<object>();

        if (definition is null)
        {
            return SurveyRuleJson.Serialize(sources);
        }

        foreach (var (_, pageIndex, question) in SurveyDefinitions.Questions(definition))
        {
            if (pageIndex > lastPageIndex || (excludeLastPage && pageIndex == lastPageIndex) || question.Id == excludeQuestionId)
            {
                continue;
            }

            var descriptor = registry.GetControl(question.ControlType);

            if (descriptor is null || !descriptor.Has(SurveyControlCapabilities.Answerable))
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(question.Title) ? question.Name : $"{question.Name}: {question.Title}";

            sources.Add(new
            {
                id = question.Id,
                label,
                choices = question.Choices.Where(choice => !choice.Retired).Select(choice => new { id = choice.Id, text = choice.Text ?? choice.Value }),
                rows = question.Rows.Where(row => !row.Retired).Select(row => new { id = row.Id, text = row.Text ?? row.Name }),
            });
        }

        foreach (var variable in definition.Variables.Where(variable => !string.IsNullOrEmpty(variable.Name)))
        {
            sources.Add(new { id = "var:" + variable.Name, label = variable.Name, choices = Array.Empty<object>(), rows = Array.Empty<object>() });
        }

        if (definition.Scoring.Enabled)
        {
            sources.Add(new { id = "score", label = "score", choices = Array.Empty<object>(), rows = Array.Empty<object>() });

            foreach (var category in definition.ScoreCategories)
            {
                sources.Add(new { id = "score:" + category.Id, label = "score: " + (category.Title ?? category.Name), choices = Array.Empty<object>(), rows = Array.Empty<object>() });
            }
        }

        return SurveyRuleJson.Serialize(sources);
    }
}
