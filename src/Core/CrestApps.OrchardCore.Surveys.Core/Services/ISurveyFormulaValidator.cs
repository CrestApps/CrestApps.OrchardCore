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

    /// <summary>
    /// Gets the fields a formula reads, as written between its brackets (<c>name</c>, <c>name.row</c>, <c>var:name</c>,
    /// <c>score</c>...), so the publish checks can tell which questions it depends on.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="formula">The formula.</param>
    /// <returns>The fields, or none when the formula is empty or doesn't compile.</returns>
    IReadOnlyCollection<string> GetReferences(SurveyDefinition definition, string formula)
        => [];
}
