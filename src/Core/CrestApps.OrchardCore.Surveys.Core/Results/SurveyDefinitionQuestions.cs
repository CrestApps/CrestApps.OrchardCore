using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// Reads the questions of a definition.
/// </summary>
public static class SurveyDefinitionQuestions
{
    /// <summary>
    /// Lists every question of a definition in designed order, pool members included.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The pages and questions.</returns>
    public static IEnumerable<(SurveyPage Page, SurveyQuestion Question)> All(SurveyDefinition definition)
    {
        if (definition?.Pages is null)
        {
            yield break;
        }

        foreach (var page in definition.Pages)
        {
            foreach (var question in page?.Questions ?? [])
            {
                if (question is not null && !string.IsNullOrEmpty(question.Id))
                {
                    yield return (page, question);
                }
            }
        }
    }

    /// <summary>
    /// Maps the questions of a definition by id; the first of duplicate ids wins.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The questions by id.</returns>
    public static IReadOnlyDictionary<string, SurveyQuestion> ById(SurveyDefinition definition)
    {
        var questions = new Dictionary<string, SurveyQuestion>(StringComparer.Ordinal);

        foreach (var (_, question) in All(definition))
        {
            questions.TryAdd(question.Id, question);
        }

        return questions;
    }
}
