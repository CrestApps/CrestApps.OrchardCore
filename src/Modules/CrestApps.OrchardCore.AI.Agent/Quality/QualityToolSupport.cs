using System.ComponentModel.DataAnnotations;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// What the test case and test run tools share: the admin links and reading the arguments a model sends.
/// </summary>
internal static class QualityToolSupport
{
    /// <summary>
    /// Gets the address of a test case's editor.
    /// </summary>
    public static string GetTestCaseEditPath(IServiceProvider services, string testCaseId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "AITestCases", "Edit", new RouteValueDictionary { ["id"] = testCaseId });

    /// <summary>
    /// Gets the address of a test run's details.
    /// </summary>
    public static string GetTestRunPath(IServiceProvider services, string runId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "AITestCases", "RunDetails", new RouteValueDictionary { ["id"] = runId });

    /// <summary>
    /// Writes the errors of a validator as one sentence.
    /// </summary>
    /// <param name="errors">The errors.</param>
    /// <returns>The sentence.</returns>
    public static string Describe(IEnumerable<ValidationResult> errors)
        => string.Join(" ", errors.Select(error => error.ErrorMessage));

    /// <summary>
    /// Trims a text and turns an empty one into <see langword="null"/>.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The trimmed text, or <see langword="null"/>.</returns>
    public static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Reads a text argument that may be given empty to clear a value.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="key">The name of the argument.</param>
    /// <param name="value">The trimmed text, or <see langword="null"/> when it is empty.</param>
    /// <returns><see langword="true"/> when the argument is given.</returns>
    public static bool TryReadText(AIFunctionArguments arguments, string key, out string value)
    {
        if (arguments.TryGetFirstString(key, allowEmptyString: true, out var text) && text is not null)
        {
            value = Normalize(text);

            return true;
        }

        value = null;

        return false;
    }
}
