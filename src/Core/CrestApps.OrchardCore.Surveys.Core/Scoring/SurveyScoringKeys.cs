using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Scoring;

/// <summary>
/// Builds the definition a response is scored with: its own version's, or, once <b>Regrade versions</b> scored it with
/// another version's answer key, its own version with that key (<see cref="SurveyResponse.ScoredWithVersionId"/>). Every
/// later scoring of the response (grading, Score again, finishing it again after a reopen) scores with the same key, so
/// none of them undoes the regrade.
/// </summary>
public static class SurveyScoringKeys
{
    /// <summary>
    /// Gets the definition to score a response with.
    /// </summary>
    /// <param name="versionCache">The version cache.</param>
    /// <param name="response">The response.</param>
    /// <param name="version">The response's own version, or <see langword="null"/> for a preview of the draft.</param>
    /// <param name="definition">The definition the response runs (its version's, or the draft's).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definition, with the other version's key where the response was regraded with one.</returns>
    public static async Task<SurveyDefinition> ScoringDefinitionAsync(
        ISurveyVersionCache versionCache,
        SurveyResponse response,
        SurveyVersion version,
        SurveyDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(versionCache);

        if (definition is null
            || version is null
            || string.IsNullOrEmpty(response?.ScoredWithVersionId)
            || string.Equals(response.ScoredWithVersionId, version.ItemId, StringComparison.Ordinal))
        {
            return definition;
        }

        var keyVersion = await versionCache.GetAsync(response.ScoredWithVersionId, cancellationToken);

        // A key version that is gone, or belongs to another survey, can't be used: the response's own key scores it.
        if (keyVersion?.Definition is null || !string.Equals(keyVersion.SurveyId, version.SurveyId, StringComparison.Ordinal))
        {
            return definition;
        }

        return WithKeyOf(definition, keyVersion.Definition).Definition;
    }

    /// <summary>
    /// Copies a definition with the answer key of another version: each question of an unchanged type takes the other
    /// version's scoring, unless that key names choices or rows the question doesn't have.
    /// </summary>
    /// <param name="definition">The definition the responses run.</param>
    /// <param name="keyDefinition">The definition whose key is used.</param>
    /// <returns>The copy, and the questions that kept their own key.</returns>
    public static (SurveyDefinition Definition, IReadOnlyList<SurveyQuestion> Kept) WithKeyOf(SurveyDefinition definition, SurveyDefinition keyDefinition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(keyDefinition);

        var copy = JsonSerializer.Deserialize<SurveyDefinition>(JsonSerializer.Serialize(definition));
        var kept = new List<SurveyQuestion>();
        var keys = keyDefinition.Pages
            .SelectMany(page => page.Questions)
            .Where(question => !string.IsNullOrEmpty(question?.Id))
            .GroupBy(question => question.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var question in copy.Pages.SelectMany(page => page.Questions))
        {
            // Only questions of an unchanged type take the other version's key: a changed type scores differently.
            if (question?.Id is null
                || !keys.TryGetValue(question.Id, out var key)
                || !string.Equals(key.ControlType, question.ControlType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // A key that names choices or rows this version does not have (a bank imported again before choices kept
            // their ids) would score every answer 0, so the question keeps its own key and its scores.
            if (!KeyFits(question, key.Scoring))
            {
                kept.Add(question);

                continue;
            }

            question.Scoring = JsonSerializer.Deserialize<SurveyQuestionScoring>(JsonSerializer.Serialize(key.Scoring ?? new SurveyQuestionScoring()));
        }

        return (copy, kept);
    }

    private static bool KeyFits(SurveyQuestion question, SurveyQuestionScoring key)
    {
        if (key is null)
        {
            return true;
        }

        // Only ids the question lists can be checked: a yes/no question keys "true" or "false", and a question whose
        // choices come from another one has none of its own.
        var choiceIds = (question.Choices ?? []).Where(choice => !string.IsNullOrEmpty(choice?.Id)).Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
        var rowIds = (question.Rows ?? []).Where(row => !string.IsNullOrEmpty(row?.Id)).Select(row => row.Id).ToHashSet(StringComparer.Ordinal);

        if (choiceIds.Count > 0)
        {
            var named = (key.CorrectChoiceIds ?? [])
                .Concat(key.ChoicePoints?.Keys ?? Enumerable.Empty<string>())
                .Concat(key.Pairs?.Values ?? Enumerable.Empty<string>());

            if (named.Any(id => !string.IsNullOrEmpty(id) && !choiceIds.Contains(id)))
            {
                return false;
            }
        }

        if (rowIds.Count > 0 && (key.Pairs?.Keys ?? Enumerable.Empty<string>()).Any(id => !string.IsNullOrEmpty(id) && !rowIds.Contains(id)))
        {
            return false;
        }

        return true;
    }
}
