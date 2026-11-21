using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// Finds the choices a question offers, following carried-forward choices of an earlier question.
/// </summary>
public static class SurveyChoiceSource
{
    /// <summary>
    /// Gets the choices of a question: its own, or those of the question it carries forward from.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="definition">The definition, or <see langword="null"/>.</param>
    /// <returns>The choices.</returns>
    public static IReadOnlyList<SurveyChoice> GetChoices(SurveyQuestion question, SurveyDefinition definition)
    {
        if (question is null)
        {
            return [];
        }

        if (string.IsNullOrEmpty(question.ChoicesFrom) || definition is null)
        {
            return question.Choices ?? [];
        }

        var source = definition.Pages?
            .SelectMany(page => page?.Questions ?? [])
            .FirstOrDefault(other => string.Equals(other?.Id, question.ChoicesFrom, StringComparison.Ordinal));

        return source?.Choices is { Count: > 0 } choices ? choices : question.Choices ?? [];
    }

    /// <summary>
    /// Keeps the carried-forward choices the mode allows, given the answer to the source question.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="choiceIds">The choice ids in display order.</param>
    /// <param name="sourceAnswer">The answer to the source question, or <see langword="null"/>.</param>
    /// <returns>The choice ids the respondent may pick.</returns>
    public static IReadOnlyList<string> FilterCarriedForward(SurveyQuestion question, IEnumerable<string> choiceIds, SurveyAnswer sourceAnswer)
    {
        var ids = (choiceIds ?? []).ToList();

        if (string.IsNullOrEmpty(question?.ChoicesFrom) || question.ChoicesFromMode == ChoicesFromMode.All)
        {
            return ids;
        }

        var chosen = (sourceAnswer?.Values ?? []).ToHashSet(StringComparer.Ordinal);

        return question.ChoicesFromMode == ChoicesFromMode.Selected
            ? ids.Where(chosen.Contains).ToList()
            : ids.Where(id => !chosen.Contains(id)).ToList();
    }

    /// <summary>
    /// Orders choices for one response: retired choices left out, shuffled when asked with pinned choices kept in
    /// place, and other and exclusive choices last.
    /// </summary>
    /// <param name="choices">The choices in designed order.</param>
    /// <param name="shuffle">Whether to shuffle.</param>
    /// <param name="random">The seeded random generator.</param>
    /// <returns>The choice ids in display order.</returns>
    public static List<string> OrderChoices(IEnumerable<SurveyChoice> choices, bool shuffle, SurveyRandom random)
    {
        var active = (choices ?? []).Where(choice => choice is not null && !choice.Retired && !string.IsNullOrEmpty(choice.Id)).ToList();
        var main = active.Where(choice => !choice.IsOther && !choice.IsExclusive).ToList();
        var last = active.Where(choice => choice.IsOther || choice.IsExclusive).ToList();

        if (shuffle && random is not null)
        {
            var movable = main.Where(choice => !choice.Pinned).ToList();
            random.Shuffle(movable);

            var next = 0;

            for (var i = 0; i < main.Count; i++)
            {
                if (!main[i].Pinned)
                {
                    main[i] = movable[next++];
                }
            }
        }

        return main.Concat(last).Select(choice => choice.Id).ToList();
    }
}
