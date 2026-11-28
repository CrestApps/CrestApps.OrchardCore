using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Formulas;

/// <summary>
/// Checks formulas with the survey formula service.
/// </summary>
public sealed class SurveyFormulaValidator : ISurveyFormulaValidator
{
    private readonly SurveyFormulaService _formulas;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyFormulaValidator"/> class.
    /// </summary>
    /// <param name="formulas">The formula service.</param>
    public SurveyFormulaValidator(SurveyFormulaService formulas)
    {
        _formulas = formulas;
    }

    /// <inheritdoc />
    public bool TryCompile(SurveyDefinition definition, string formula, out string error)
        => _formulas.TryCompile(definition, formula, out error);

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetReferences(SurveyDefinition definition, string formula)
        => _formulas.GetReferences(definition, formula);
}
