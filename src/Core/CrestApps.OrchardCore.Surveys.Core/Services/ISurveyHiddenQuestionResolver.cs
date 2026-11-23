using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Tells which questions of a response were hidden by logic, so scoring leaves them out of the most points (a locked
/// answer, checked with immediate feedback, still counts). The default hides nothing; the logic engine replaces it.
/// </summary>
public interface ISurveyHiddenQuestionResolver
{
    /// <summary>
    /// Gets the ids of the questions hidden for a response.
    /// </summary>
    /// <param name="definition">The definition of the response's version.</param>
    /// <param name="response">The response.</param>
    /// <returns>The question ids.</returns>
    IReadOnlySet<string> GetHiddenQuestionIds(SurveyDefinition definition, SurveyResponse response);
}
