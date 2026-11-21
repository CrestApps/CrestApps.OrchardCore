using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Offers survey templates in the new survey page. Register an implementation with
/// <c>services.AddSurveyTemplateProvider&lt;T&gt;()</c>.
/// </summary>
public interface ISurveyTemplateProvider
{
    /// <summary>
    /// Gets the templates the provider offers.
    /// </summary>
    /// <returns>The templates.</returns>
    IEnumerable<SurveyTemplateDescriptor> GetTemplates();

    /// <summary>
    /// Builds the definition of one of the provider's templates, with new ids.
    /// </summary>
    /// <param name="name">The template name.</param>
    /// <returns>The definition, or <see langword="null"/> when the template is not the provider's.</returns>
    SurveyDefinition Build(string name);
}
