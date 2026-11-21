using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Compares two survey definitions by stable ids.
/// </summary>
public interface ISurveyDefinitionDiffer
{
    /// <summary>
    /// Compares two definitions.
    /// </summary>
    /// <param name="before">The older definition, or <see langword="null"/>.</param>
    /// <param name="after">The newer definition.</param>
    /// <returns>The changes.</returns>
    SurveyDefinitionDiff Compare(SurveyDefinition before, SurveyDefinition after);
}
