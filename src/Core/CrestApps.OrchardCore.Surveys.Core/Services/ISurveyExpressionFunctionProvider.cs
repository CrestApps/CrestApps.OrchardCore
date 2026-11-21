using OrchardCore.DataSources.Expressions;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Offers functions to survey formulas only (pipeline editors do not list them). Register an implementation with
/// <c>services.AddSurveyExpressionFunctionProvider&lt;T&gt;()</c>.
/// </summary>
public interface ISurveyExpressionFunctionProvider
{
    /// <summary>
    /// Gets the functions.
    /// </summary>
    /// <returns>The functions.</returns>
    IEnumerable<ExpressionFunction> GetFunctions();
}
