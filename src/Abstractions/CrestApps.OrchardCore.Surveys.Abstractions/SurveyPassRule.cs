using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The one rule that decides whether a percent passes a survey. The score band the percent falls in decides first; a
/// band that follows the pass mark, or no band, leaves it to the pass mark; with neither the survey makes no pass
/// decision. Surveys sets a response's result with it, and a module that combines several responses into one score,
/// such as a Learning quiz, decides that score's pass with it too, so the two never disagree.
/// </summary>
public static class SurveyPassRule
{
    /// <summary>
    /// Finds the score band a percent falls in: the band with the highest minimum the percent reaches.
    /// </summary>
    /// <param name="definition">The survey definition.</param>
    /// <param name="percent">The percent, or <see langword="null"/>.</param>
    /// <returns>The band, or <see langword="null"/> when there is no percent or no band it reaches.</returns>
    public static ScoreBand FindBand(SurveyDefinition definition, decimal? percent)
    {
        if (definition is null || percent is not { } value)
        {
            return null;
        }

        return (definition.ScoreBands ?? [])
            .Where(candidate => candidate is not null && candidate.MinPercent <= value)
            .OrderByDescending(candidate => candidate.MinPercent)
            .FirstOrDefault();
    }

    /// <summary>
    /// Decides whether a graded percent passes: its band's own pass or fail, otherwise the pass mark.
    /// </summary>
    /// <param name="definition">The survey definition.</param>
    /// <param name="percent">The graded percent, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> or <see langword="false"/>, or <see langword="null"/> when there is no percent or the survey
    /// has neither a deciding band nor a pass mark. No pass mark is never a pass.
    /// </returns>
    public static bool? Decide(SurveyDefinition definition, decimal? percent)
    {
        if (definition is null || percent is not { } value)
        {
            return null;
        }

        return FindBand(definition, value)?.Passing
            ?? (definition.Scoring?.PassMarkPercent is { } mark ? value >= mark : null);
    }
}
