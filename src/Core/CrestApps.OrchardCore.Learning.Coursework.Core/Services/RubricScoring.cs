using CrestApps.OrchardCore.Learning.Coursework.Models;

namespace CrestApps.OrchardCore.Learning.Coursework.Core.Services;

/// <summary>
/// Scores rubric choices: the total is the sum of the points of the level chosen for each criterion, out of the sum of
/// each criterion's best level.
/// </summary>
public static class RubricScoring
{
    /// <summary>
    /// Scores the chosen levels against a rubric. A choice that names a criterion the rubric does not have, or a level
    /// of another criterion, counts for nothing and leaves the score incomplete.
    /// </summary>
    /// <param name="rubric">The rubric.</param>
    /// <param name="choices">The level chosen for each criterion, by criterion id.</param>
    /// <returns>The points, the maximum, the percentage and whether every criterion has a valid level.</returns>
    public static RubricScore Score(RubricPart rubric, IDictionary<string, string> choices)
    {
        ArgumentNullException.ThrowIfNull(rubric);

        var criteria = rubric.Criteria ?? [];
        var complete = criteria.Count > 0;
        decimal points = 0;

        foreach (var criterion in criteria)
        {
            var level = choices is not null && criterion.Id is not null && choices.TryGetValue(criterion.Id, out var levelId)
                ? (criterion.Levels ?? []).FirstOrDefault(candidate => string.Equals(candidate.Id, levelId, StringComparison.Ordinal))
                : null;

            if (level is null)
            {
                complete = false;

                continue;
            }

            points += level.Points;
        }

        var max = MaxPoints(rubric);
        var percent = max > 0 ? Math.Round(points * 100 / max, 2, MidpointRounding.AwayFromZero) : 0;

        return new RubricScore(points, max, percent, complete);
    }

    /// <summary>
    /// Gets the most points a rubric gives: the sum of each criterion's best level.
    /// </summary>
    /// <param name="rubric">The rubric.</param>
    /// <returns>The points.</returns>
    public static decimal MaxPoints(RubricPart rubric)
    {
        ArgumentNullException.ThrowIfNull(rubric);

        return (rubric.Criteria ?? [])
            .Select(criterion => (criterion.Levels ?? []).Select(level => level.Points).DefaultIfEmpty(0).Max())
            .Sum(best => Math.Max(best, 0));
    }
}

/// <summary>
/// A rubric score.
/// </summary>
/// <param name="Points">The points of the chosen levels.</param>
/// <param name="MaxPoints">The most points the rubric gives.</param>
/// <param name="Percent">The points as a percentage of the maximum, rounded to two decimals.</param>
/// <param name="Complete">Whether every criterion has a level of its own chosen.</param>
public sealed record RubricScore(decimal Points, decimal MaxPoints, decimal Percent, bool Complete);
