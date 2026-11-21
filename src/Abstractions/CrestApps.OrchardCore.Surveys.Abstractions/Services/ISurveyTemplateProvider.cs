using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Offers survey templates in the new survey page. The built-in provider offers a blank survey and every stored
/// <see cref="SurveyTemplate"/>; register another with <c>services.AddSurveyTemplateProvider&lt;T&gt;()</c> to offer
/// templates from code.
/// </summary>
public interface ISurveyTemplateProvider
{
    /// <summary>
    /// Gets the templates the provider offers.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The templates.</returns>
    Task<IEnumerable<SurveyTemplateDescriptor>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the definition of one of the provider's templates. The caller gives it new ids.
    /// </summary>
    /// <param name="name">The template name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definition, or <see langword="null"/> when the template is not the provider's.</returns>
    Task<SurveyDefinition> BuildAsync(string name, CancellationToken cancellationToken = default);
}
