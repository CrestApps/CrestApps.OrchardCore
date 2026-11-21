using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Checks a survey definition: built-in checks, every question type's checks and every contributor.
/// </summary>
public interface ISurveyDefinitionValidator
{
    /// <summary>
    /// Checks a definition.
    /// </summary>
    /// <param name="survey">The survey, or <see langword="null"/>.</param>
    /// <param name="definition">The definition.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The context with the problems found.</returns>
    Task<SurveyDefinitionValidationContext> ValidateAsync(Survey survey, SurveyDefinition definition, CancellationToken cancellationToken = default);
}
