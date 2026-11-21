using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Logic;

/// <summary>
/// Renders the message of an ending: piping, then Liquid with a model of the survey, the response, the answers and the
/// score, sanitized. The module implements it with Orchard Core's Liquid manager.
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
    /// <returns>The HTML.</returns>
    Task<string> RenderAsync(Survey survey, SurveyDefinition definition, SurveyResponse response, SurveyEnding ending);
}
