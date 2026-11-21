using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Checks that a formula compiles against a survey definition, for the publish checks.
/// </summary>
public interface ISurveyFormulaValidator
{
    /// <summary>
    /// Compiles a formula against a definition.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="formula">The formula.</param>
    /// <param name="error">The error when it does not compile.</param>
    /// <returns><see langword="true"/> when it compiles.</returns>
    bool TryCompile(SurveyDefinition definition, string formula, out string error);
}
