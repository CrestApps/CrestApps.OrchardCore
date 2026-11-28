using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Scores a response with a definition's key.
/// </summary>
public interface ISurveyScoringEngine
{
    /// <summary>
    /// Scores every shown question of a response and sets its totals, category scores, percent, pass and band. Grader
    /// scores are kept.
    /// </summary>
    /// <param name="definition">The definition whose key is used.</param>
    /// <param name="response">The response; its answers and totals are updated.</param>
    void Score(SurveyDefinition definition, SurveyResponse response);

    /// <summary>
    /// Reads what scoring needs before it runs, such as the site's time zone that rules on the date or time read. A
    /// caller that scores outside a respondent's request (a regrade) calls it first.
    /// </summary>
    /// <returns>A task.</returns>
    Task PrepareAsync()
        => Task.CompletedTask;
}
