using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Provides URI template matching and building utilities for MCP resources.
/// Supports matching incoming URIs against URI templates with named variables
/// (e.g., "recipe-step-schema://my-resource/{stepName}") and extracting variable values.
/// </summary>
public static partial class McpResourceUri
{
    /// <summary>
    /// Attempts to match an actual URI against a URI template pattern and extract variable values.
    /// For example, template "recipe-step-schema://my-resource/{stepName}" matched against
    /// "recipe-step-schema://my-resource/feature" yields { "stepName": "feature" }.
    /// </summary>
    /// <param name="uriTemplate">The URI template pattern containing {variable} placeholders.</param>
    /// <param name="actualUri">The actual URI to match against the template.</param>
    /// <param name="variables">When successful, the extracted variable name-value pairs.</param>
    /// <returns><c>true</c> if the URI matches the template; otherwise, <c>false</c>.</returns>
    public static bool TryMatch(string uriTemplate, string actualUri, out IReadOnlyDictionary<string, string> variables)
    {
        variables = null;

        if (string.IsNullOrWhiteSpace(uriTemplate) || string.IsNullOrWhiteSpace(actualUri))
        {
            return false;
        }

        uriTemplate = uriTemplate.Trim();
        actualUri = actualUri.Trim();

        // Collect all variable matches first so we know which is the last one.
        var matches = new List<(int Index, int Length, string Name)>();

        foreach (var match in VariablePlaceholderRegex().EnumerateMatches(uriTemplate))
        {
            var varName = uriTemplate[(match.Index + 1)..(match.Index + match.Length - 1)];
            matches.Add((match.Index, match.Length, varName));
        }

        // Build a regex by splitting the template into literal segments and variable placeholders.
        // This avoids relying on Regex.Escape behavior for { and } characters.
        var variableNames = new List<string>();
        var regexBuilder = new StringBuilder("^");
        var lastIndex = 0;

        for (var i = 0; i < matches.Count; i++)
        {
            var (index, length, varName) = matches[i];

            // Escape the literal part before this variable.
            if (index > lastIndex)
            {
                regexBuilder.Append(Regex.Escape(uriTemplate[lastIndex..index]));
            }

            variableNames.Add(varName);

            // The last variable in the template uses .+ to allow multi-segment paths (e.g., "docs/report.pdf").
            // All other variables use [^/]+ to match a single path segment.
            var capturePattern = i == matches.Count - 1 ? ".+" : "[^/]+";
            regexBuilder.Append($"(?<{varName}>{capturePattern})");

            lastIndex = index + length;
        }

        // Append any remaining literal text after the last variable.
        if (lastIndex < uriTemplate.Length)
        {
            regexBuilder.Append(Regex.Escape(uriTemplate[lastIndex..]));
        }

        regexBuilder.Append('$');

        var regex = new Regex(regexBuilder.ToString(), RegexOptions.IgnoreCase);
        var regexMatch = regex.Match(actualUri);

        if (!regexMatch.Success)
        {
            return false;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in variableNames)
        {
            var group = regexMatch.Groups[name];

            if (group.Success)
            {
                result[name] = Uri.UnescapeDataString(group.Value);
            }
        }

        variables = result;

        return true;
    }

    /// <summary>
    /// Determines whether the given URI contains template variables (e.g., {name}).
    /// </summary>
    public static bool IsTemplate(string uri)
    {
        return !string.IsNullOrWhiteSpace(uri) && uri.AsSpan().Trim().Contains('{');
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex VariablePlaceholderRegex();
}
