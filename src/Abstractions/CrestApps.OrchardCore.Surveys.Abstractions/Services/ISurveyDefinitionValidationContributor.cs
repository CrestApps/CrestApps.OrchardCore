using CrestApps.OrchardCore.Surveys.Controls;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Adds checks of a consumer to survey definitions, such as Learning's rules for quiz course items. Register an
/// implementation with <c>services.AddSurveyDefinitionValidationContributor&lt;T&gt;()</c>.
/// </summary>
public interface ISurveyDefinitionValidationContributor
{
    /// <summary>
    /// Checks a definition, adding problems to the context.
    /// </summary>
    /// <param name="context">The validation context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task ValidateAsync(SurveyDefinitionValidationContext context, CancellationToken cancellationToken = default);
}
