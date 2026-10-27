using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.ViewModels;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Lists everything two copies of an AI profile hold, side by side: every field and every setting, grouped like the
/// tabs of the profile editor, so a setting added by any feature is compared without being listed here.
/// </summary>
internal static class AIProfileVersionComparer
{
    /// <summary>
    /// The groups rows are shown in, in order.
    /// </summary>
    public static readonly string[] Sections =
    [
        General,
        Instructions,
        Parameters,
        Capabilities,
        PromptSecurity,
        DataProcessing,
        Other,
    ];

    public const string General = "General";
    public const string Instructions = "Instructions";
    public const string Parameters = "Parameters";
    public const string Capabilities = "Capabilities";
    public const string PromptSecurity = "Prompt Security";
    public const string DataProcessing = "Data Processing & Metrics";
    public const string Other = "Other";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // What identifies the profile or changes on every save, rather than what it does.
    private static readonly HashSet<string> _skippedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(AIProfile.ItemId),
        nameof(AIProfile.Name),
        nameof(AIProfile.CreatedUtc),
        "ModifiedUtc",
        nameof(AIProfile.OwnerId),
        nameof(AIProfile.Author),
        nameof(AIProfile.Properties),
        nameof(AIProfile.Settings),
        nameof(AIProfileVersionMetadata),
    };

    private static readonly Dictionary<string, string> _fieldLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(AIProfile.DisplayText)] = "Title",
        [nameof(AIProfile.ChatDeploymentName)] = "Chat deployment",
        [nameof(AIProfile.UtilityDeploymentName)] = "Utility deployment",
        [nameof(AIProfile.OrchestratorName)] = "Orchestrator",
        [nameof(AIProfile.TitleType)] = "Session title",
        [nameof(AIProfile.PromptTemplate)] = "Prompt template",
        [nameof(AIProfile.PromptSubject)] = "Prompt subject",
        ["TopP"] = "Top P",
        ["PastMessagesCount"] = "Past messages",
    };

    // The fields of AIProfileMetadata that belong on the Instructions tab; the rest are parameters.
    private static readonly HashSet<string> _instructionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "SystemMessage",
        "InitialPrompt",
    };

    /// <summary>
    /// Lists both copies' fields and settings, in the order of <see cref="Sections"/>.
    /// </summary>
    /// <param name="then">The older copy, or the one on the left.</param>
    /// <param name="now">The newer copy, or the one on the right.</param>
    /// <param name="templateTitles">Titles of prompt templates by identifier, shown instead of the identifiers a
    /// profile keeps for its prompt templates, speaking styles and guardrail topics.</param>
    public static List<AIProfileVersionCompareRow> Compare(AIProfile then, AIProfile now, IReadOnlyDictionary<string, string> templateTitles = null)
    {
        ArgumentNullException.ThrowIfNull(then);
        ArgumentNullException.ThrowIfNull(now);

        var thenRows = Flatten(then, templateTitles);
        var nowRows = Flatten(now, templateTitles);
        var keys = thenRows.Keys.Union(nowRows.Keys).ToList();

        return keys
            .Select((key, index) => (key, index))
            .OrderBy(item => Array.IndexOf(Sections, item.key.Section))
            .ThenBy(item => item.index)
            .Select(item =>
            {
                thenRows.TryGetValue(item.key, out var thenValue);
                nowRows.TryGetValue(item.key, out var nowValue);

                var isCode = thenValue.IsCode || nowValue.IsCode;
                var multiline = isCode || IsLong(thenValue.Text) || IsLong(nowValue.Text);

                return new AIProfileVersionCompareRow(item.key.Label, thenValue.Text, nowValue.Text, multiline, isCode)
                {
                    Section = item.key.Section,
                };
            })
            .ToList();
    }

    private static Dictionary<(string Section, string Label), (string Text, bool IsCode)> Flatten(AIProfile profile, IReadOnlyDictionary<string, string> templateTitles)
    {
        var rows = new Dictionary<(string Section, string Label), (string Text, bool IsCode)>();

        if (JsonSerializer.SerializeToNode(profile, _jsonOptions) is JsonObject fields)
        {
            foreach (var (name, value) in fields)
            {
                if (!_skippedFields.Contains(name))
                {
                    rows[(General, LabelOf(name))] = Format(value, templateTitles);
                }
            }
        }

        AddEntries(rows, profile.Properties, templateTitles);
        AddEntries(rows, profile.Settings, templateTitles);

        return rows;
    }

    private static void AddEntries(Dictionary<(string Section, string Label), (string Text, bool IsCode)> rows, IDictionary<string, object> entries, IReadOnlyDictionary<string, string> templateTitles)
    {
        if (entries is null)
        {
            return;
        }

        foreach (var (key, entry) in entries)
        {
            if (_skippedFields.Contains(key))
            {
                continue;
            }

            var node = entry as JsonNode ?? JsonSerializer.SerializeToNode(entry, _jsonOptions);

            if (node is not JsonObject properties)
            {
                rows[(SectionOf(key, null), GroupOf(key))] = Format(node, templateTitles);

                continue;
            }

            foreach (var (name, value) in properties)
            {
                // The main settings of the profile read as fields of their tab rather than of a group.
                var label = key == nameof(AIProfileMetadata)
                    ? LabelOf(name)
                    : GroupOf(key) + " › " + LabelOf(name);

                rows[(SectionOf(key, name), label)] = Format(value, templateTitles);
            }
        }
    }

    internal static string SectionOf(string key, string field)
    {
        if (key == nameof(AIProfileMetadata))
        {
            return field is not null && _instructionFields.Contains(field) ? Instructions : Parameters;
        }

        if (Contains(key, "ChatProfile") || Contains(key, "ChatMode"))
        {
            return General;
        }

        if (Contains(key, "PromptTemplate") || Contains(key, "SpeakingStyle") || Contains(key, "Persona"))
        {
            return Instructions;
        }

        if (Contains(key, "DeploymentParameters"))
        {
            return Parameters;
        }

        if (Contains(key, "PromptSecurity") || Contains(key, "Guardrail"))
        {
            return PromptSecurity;
        }

        if (Contains(key, "DataExtraction") || Contains(key, "PostSession") || Contains(key, "ContactMemory") ||
            Contains(key, "DataPrivacy") || Contains(key, "Analytics") || Contains(key, "Evaluation") || Contains(key, "Metric"))
        {
            return DataProcessing;
        }

        if (Contains(key, "Tool") || Contains(key, "Function") || Contains(key, "Agent") || Contains(key, "Mcp") ||
            Contains(key, "DataSource") || Contains(key, "Document") || Contains(key, "Memory") || Contains(key, "A2A"))
        {
            return Capabilities;
        }

        return Other;
    }

    // "AIProfileGuardrailSettings" reads as "Guardrail", "FunctionInvocationMetadata" as "Function Invocation".
    internal static string GroupOf(string key)
    {
        var name = key;

        foreach (var suffix in new[] { "ProfileSettings", "Settings", "Metadata" })
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name[..^suffix.Length];

                break;
            }
        }

        if (name.Length > "AIProfile".Length && name.StartsWith("AIProfile", StringComparison.Ordinal))
        {
            name = name["AIProfile".Length..];
        }

        return Humanize(name);
    }

    internal static string LabelOf(string name)
    {
        if (_fieldLabels.TryGetValue(name, out var label))
        {
            return label;
        }

        var words = Humanize(name);

        // "Check Customer" reads as "Check customer", keeping initialisms such as "AI" and "URL".
        return string.Join(' ', words.Split(' ').Select((word, index) =>
            index == 0 || word.Length < 2 || word.All(char.IsUpper) ? word : char.ToLowerInvariant(word[0]) + word[1..]));
    }

    internal static (string Text, bool IsCode) Format(JsonNode node, IReadOnlyDictionary<string, string> templateTitles)
    {
        switch (node)
        {
            case null:
                return (null, false);

            case JsonValue value when value.TryGetValue<bool>(out var flag):
                return (flag ? "Yes" : "No", false);

            case JsonValue value when value.TryGetValue<string>(out var text):
                return (string.IsNullOrWhiteSpace(text) ? null : TitleOf(text, templateTitles), false);

            case JsonValue value:
                return (value.ToJsonString(), false);

            case JsonArray array when array.Count == 0:
                return (null, false);

            case JsonArray array when array.All(item => item is JsonValue):
                return (string.Join(", ", array.Select(item => Format(item, templateTitles).Text).Where(text => text is not null)), false);

            case JsonObject properties when properties.Count == 0:
                return (null, false);

            default:
                return (node.ToJsonString(_jsonOptions), true);
        }
    }

    private static string TitleOf(string text, IReadOnlyDictionary<string, string> templateTitles)
        => templateTitles is not null && templateTitles.TryGetValue(text, out var title) && !string.Equals(title, text, StringComparison.Ordinal)
            ? title + " (" + text + ")"
            : text;

    private static bool IsLong(string text)
        => text is not null && (text.Length > 80 || text.Contains('\n'));

    private static bool Contains(string key, string part)
        => key.Contains(part, StringComparison.OrdinalIgnoreCase);

    private static string Humanize(string key)
    {
        var builder = new StringBuilder(key.Length + 8);

        for (var i = 0; i < key.Length; i++)
        {
            if (i > 0 && char.IsUpper(key[i]) && (char.IsLower(key[i - 1]) || (i + 1 < key.Length && char.IsLower(key[i + 1]) && char.IsUpper(key[i - 1]))))
            {
                builder.Append(' ');
            }

            builder.Append(key[i]);
        }

        return builder.ToString();
    }
}
