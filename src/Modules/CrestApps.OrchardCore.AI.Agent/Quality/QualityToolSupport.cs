using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// What the test case, alert rule and QA cohort tools share: the admin links and reading the arguments a model sends.
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
    /// Gets the address of an alert rule's editor.
    /// </summary>
    public static string GetAlertRuleEditPath(IServiceProvider services, string ruleId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "AIAlertRules", "Edit", new RouteValueDictionary { ["id"] = ruleId });

    /// <summary>
    /// Gets the address of a QA cohort's editor.
    /// </summary>
    public static string GetCohortEditPath(IServiceProvider services, string cohortId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, "AIQACohorts", "Edit", new RouteValueDictionary { ["id"] = cohortId });

    /// <summary>
    /// Reads a whole number argument.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="key">The name of the argument.</param>
    /// <param name="value">The number, or <see langword="null"/> when the argument is not given.</param>
    /// <returns><see langword="false"/> when the argument is given but is not a whole number.</returns>
    public static bool TryReadInt(AIFunctionArguments arguments, string key, out int? value)
    {
        value = null;

        if (!arguments.TryGetFirst(key, out object raw) || raw is null)
        {
            return true;
        }

        if (arguments.TryGetFirst<int>(key, out var number))
        {
            value = number;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads a number argument.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="key">The name of the argument.</param>
    /// <param name="value">The number, or <see langword="null"/> when the argument is not given.</param>
    /// <returns><see langword="false"/> when the argument is given but is not a number.</returns>
    public static bool TryReadDouble(AIFunctionArguments arguments, string key, out double? value)
    {
        value = null;

        if (!arguments.TryGetFirst(key, out object raw) || raw is null)
        {
            return true;
        }

        if (arguments.TryGetFirst<double>(key, out var number) && double.IsFinite(number))
        {
            value = number;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads a yes or no argument.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="key">The name of the argument.</param>
    /// <returns>The value, or <see langword="null"/> when the argument is not given.</returns>
    public static bool? ReadBool(AIFunctionArguments arguments, string key)
        => arguments.TryGetFirst<bool>(key, out var value) ? value : null;

    /// <summary>
    /// Reads an argument that holds names and texts, such as test variables.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="key">The name of the argument.</param>
    /// <param name="value">The names and texts, or <see langword="null"/> when the argument is not given.</param>
    /// <returns><see langword="false"/> when the argument is given but is not an object of texts.</returns>
    public static bool TryReadTextMap(AIFunctionArguments arguments, string key, out Dictionary<string, string> value)
    {
        value = null;

        if (!AgentToolArguments.TryReadJsonObject(arguments, key, out var json))
        {
            return false;
        }

        if (json is null)
        {
            return true;
        }

        value = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, node) in json)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            value[name.Trim()] = node switch
            {
                null => string.Empty,
                JsonValue text when text.GetValueKind() == JsonValueKind.String => text.GetValue<string>().Trim(),
                _ => node.ToJsonString(),
            };
        }

        return true;
    }

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
