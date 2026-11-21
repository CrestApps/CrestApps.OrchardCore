using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Gives values to condition sources named <c>context:{key}</c>, read from what a consumer stored on the response.
/// Register an implementation with <c>services.AddSurveyConditionSourceProvider&lt;T&gt;()</c>.
/// </summary>
public interface ISurveyConditionSourceProvider
{
    /// <summary>
    /// Gets the value of a context key, or <see langword="null"/> when the provider does not know it.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="key">The key after <c>context:</c>.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    ConditionValue GetValue(SurveyResponse response, string key);
}
