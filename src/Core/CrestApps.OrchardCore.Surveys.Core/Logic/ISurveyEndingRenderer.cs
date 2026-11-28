using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Logic;

/// <summary>
/// Renders the message of an ending: the author's message as Liquid with a model of the survey, the response, the
/// answers and the score, then piping, sanitized. A respondent's text must never reach the Liquid parser. The module
/// implements it with Orchard Core's Liquid manager.
/// </summary>
public interface ISurveyEndingRenderer
{
    /// <summary>
    /// Renders an ending's message as sanitized HTML.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="definition">The definition of the response's version.</param>
    /// <param name="response">The response.</param>
    /// <param name="ending">The ending.</param>
    /// <param name="showScore">Whether the respondent may see the score now (see <see cref="Services.ISurveyReviewPolicy"/>):
    /// when not, the score, the percent, the category scores and whether the response passed are left out of the model
    /// and the piping.</param>
    /// <returns>The HTML.</returns>
    Task<string> RenderAsync(Survey survey, SurveyDefinition definition, SurveyResponse response, SurveyEnding ending, bool showScore);
}
