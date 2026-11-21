using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// Implemented by a question type handler whose answer comes from a formula. The runtime evaluates the formula after
/// every page post and keeps the result, written in the invariant culture, in the response's variables under the
/// question name, before it reads the page's answers.
/// </summary>
public interface ISurveyCalculatedControl
{
    /// <summary>
    /// Gets the formula of a question.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The formula, or <see langword="null"/>.</returns>
    string GetFormula(SurveyQuestion question);
}
