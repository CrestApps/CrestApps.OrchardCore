using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.WebMcp.Models;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Checks the page tools a browser reports before the AI chat may use them. The browser is not trusted: a tool
/// whose name an AI provider would refuse, or whose description or schema is missing, broken or too large, is dropped.
/// </summary>
internal static partial class WebMcpBrowserToolValidator
{
    /// <summary>
    /// The most tools a browser tab may offer.
    /// </summary>
    public const int MaxTools = 64;

    /// <summary>
    /// The longest description a tool may have, in characters.
    /// </summary>
    public const int MaxDescriptionLength = 2000;

    /// <summary>
    /// The largest input schema a tool may have, in characters of JSON.
    /// </summary>
    public const int MaxSchemaLength = 16 * 1024;

    private static readonly JsonElement _emptySchema = JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone();

    /// <summary>
    /// Keeps the valid tools, the first of each name, up to <see cref="MaxTools"/>.
    /// </summary>
    /// <param name="descriptors">The tools the browser reports.</param>
    /// <returns>The valid tools.</returns>
    public static IReadOnlyList<WebMcpBrowserTool> Validate(IEnumerable<WebMcpBrowserToolDescriptor> descriptors)
    {
        var tools = new List<WebMcpBrowserTool>();

        if (descriptors is null)
        {
            return tools;
        }

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in descriptors)
        {
            if (tools.Count >= MaxTools)
            {
                break;
            }

            if (descriptor is null ||
                string.IsNullOrEmpty(descriptor.Name) ||
                !ToolNameRegex().IsMatch(descriptor.Name) ||
                string.IsNullOrWhiteSpace(descriptor.Description) ||
                descriptor.Description.Length > MaxDescriptionLength ||
                !TryParseSchema(descriptor.InputSchema, out var schema) ||
                !seenNames.Add(descriptor.Name))
            {
                continue;
            }

            tools.Add(new WebMcpBrowserTool
            {
                Name = descriptor.Name,
                Title = string.IsNullOrWhiteSpace(descriptor.Title) ? descriptor.Name : descriptor.Title.Trim(),
                Description = descriptor.Description.Trim(),
                InputSchema = schema,
            });
        }

        return tools;
    }

    private static bool TryParseSchema(string json, out JsonElement schema)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            schema = _emptySchema;

            return true;
        }

        schema = default;

        if (json.Length > MaxSchemaLength)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            schema = document.RootElement.Clone();

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]{1,64}$")]
    private static partial Regex ToolNameRegex();
}
