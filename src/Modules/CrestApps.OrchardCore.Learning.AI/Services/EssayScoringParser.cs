using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Coursework.Models;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// A suggested score and feedback for a grader. It is only a suggestion: it fills the grading form and the grader
/// confirms or changes it.
/// </summary>
public sealed class EssayScoringSuggestion
{
    /// <summary>
    /// Gets or sets the suggested points, or <see langword="null"/> when the work is graded with a rubric or the model
    /// gave no usable number.
    /// </summary>
    public decimal? Points { get; set; }

    /// <summary>
    /// Gets the suggested rubric level of each criterion, by criterion id.
    /// </summary>
    public IDictionary<string, string> Choices { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the suggested feedback to the learner.
    /// </summary>
    public string Feedback { get; set; }
}

/// <summary>
/// Reads the model's suggestion and keeps only what the grading form accepts: points within the work's range, levels
/// that belong to their criterion, and feedback within the form's length. Anything else is left out, never guessed.
/// </summary>
internal static class EssayScoringParser
{
    /// <summary>
    /// Reads a suggestion.
    /// </summary>
    /// <param name="text">The model's answer.</param>
    /// <param name="maxPoints">The most points the work can get.</param>
    /// <param name="criteria">The rubric's criteria, or <see langword="null"/> for work graded with points.</param>
    /// <param name="maxFeedbackLength">The longest feedback the form takes.</param>
    /// <returns>The suggestion, or <see langword="null"/> when nothing in it is usable.</returns>
    public static EssayScoringSuggestion Parse(string text, decimal maxPoints, IReadOnlyList<RubricCriterion> criteria, int maxFeedbackLength)
    {
        var root = AuthoringDraftParser.ReadObject(text);

        if (root is null)
        {
            return null;
        }

        var suggestion = new EssayScoringSuggestion
        {
            Feedback = AuthoringDraftParser.String(root["feedback"], Math.Max(1, maxFeedbackLength)),
        };

        if (criteria is { Count: > 0 })
        {
            if (root["choices"] is JsonObject choices)
            {
                foreach (var criterion in criteria)
                {
                    var levelId = AuthoringDraftParser.String(choices[criterion.Id ?? string.Empty], 200);

                    if (levelId is not null && criterion.Levels.Any(level => string.Equals(level.Id, levelId, StringComparison.Ordinal)))
                    {
                        suggestion.Choices[criterion.Id] = levelId;
                    }
                }
            }
        }
        else if (AuthoringDraftParser.Number(root["points"]) is { } points && points >= 0 && points <= maxPoints)
        {
            suggestion.Points = Math.Round(points, 2, MidpointRounding.AwayFromZero);
        }

        return suggestion.Points is null && suggestion.Choices.Count == 0 && suggestion.Feedback is null ? null : suggestion;
    }
}
