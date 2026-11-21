using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Accepts every formula; replaced by the runtime's compiler.
/// </summary>
public sealed class NullSurveyFormulaValidator : ISurveyFormulaValidator
{
    /// <inheritdoc />
    public bool TryCompile(SurveyDefinition definition, string formula, out string error)
    {
        error = null;

        return true;
    }
}
