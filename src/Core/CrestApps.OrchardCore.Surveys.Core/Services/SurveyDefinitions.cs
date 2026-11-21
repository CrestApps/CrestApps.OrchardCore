using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Helpers that read and prepare survey definitions.
/// </summary>
public static class SurveyDefinitions
{
    /// <summary>
    /// Makes a new empty page with a new id.
    /// </summary>
    /// <param name="number">The page number, used for its variable code.</param>
    /// <param name="title">The title, or <see langword="null"/>.</param>
    /// <returns>The page.</returns>
    public static SurveyPage NewPage(int number, string title = null)
        => new()
        {
            Id = IdGenerator.GenerateId(),
            Name = $"page_{number}",
            Title = title,
        };

    /// <summary>
    /// Makes the default ending.
    /// </summary>
    /// <param name="message">The thank-you message.</param>
    /// <returns>The ending.</returns>
    public static SurveyEnding NewDefaultEnding(string message)
        => new()
        {
            Id = IdGenerator.GenerateId(),
            Name = "thank_you",
            Title = "Thank you",
            Message = message,
            IsDefault = true,
        };

    /// <summary>
    /// Makes sure a definition has a page and a default ending.
    /// </summary>
    /// <param name="definition">The definition, changed in place.</param>
    public static void EnsureBasics(SurveyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Pages.Count == 0)
        {
            definition.Pages.Add(NewPage(1));
        }

        if (!definition.Endings.Any(ending => ending.IsDefault))
        {
            if (definition.Endings.Count > 0)
            {
                definition.Endings[0].IsDefault = true;
            }
            else
            {
                definition.Endings.Add(NewDefaultEnding("<p>Thank you for your response.</p>"));
            }
        }
    }

    /// <summary>
    /// Lists every question of a definition in designed order, with its page.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The pages and questions.</returns>
    public static IEnumerable<(SurveyPage Page, int PageIndex, SurveyQuestion Question)> Questions(SurveyDefinition definition)
    {
        if (definition?.Pages is null)
        {
            yield break;
        }

        for (var i = 0; i < definition.Pages.Count; i++)
        {
            var page = definition.Pages[i];

            foreach (var question in page.Questions ?? [])
            {
                yield return (page, i, question);
            }
        }
    }

    /// <summary>
    /// Finds a question by id.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="questionId">The id.</param>
    /// <returns>The question, or <see langword="null"/>.</returns>
    public static SurveyQuestion FindQuestion(SurveyDefinition definition, string questionId)
        => string.IsNullOrEmpty(questionId) ? null : Questions(definition).Select(item => item.Question).FirstOrDefault(question => question.Id == questionId);

    /// <summary>
    /// Makes a variable code that no question or variable of the definition uses yet.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="prefix">The prefix, such as q.</param>
    /// <returns>The code.</returns>
    public static string NextQuestionName(SurveyDefinition definition, string prefix = "q")
    {
        var used = new HashSet<string>(
            Questions(definition).Select(item => item.Question.Name).Concat(definition.Variables.Select(variable => variable.Name)).Where(name => name is not null),
            StringComparer.OrdinalIgnoreCase);

        for (var i = 1; ; i++)
        {
            var candidate = $"{prefix}{i}";

            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
