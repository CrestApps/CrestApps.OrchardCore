using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Conditions;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Writes and reads the condition rules the rule builder edits in hidden inputs.
/// </summary>
internal static class SurveyRuleJson
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Writes a rule, or an empty string for none.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The JSON.</returns>
    public static string Write(ConditionRule rule)
        => ConditionRule.IsEmpty(rule) ? string.Empty : JsonSerializer.Serialize(rule, _options);

    /// <summary>
    /// Reads a rule, or <see langword="null"/> for an empty or invalid value.
    /// </summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The rule.</returns>
    public static ConditionRule Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var rule = JsonSerializer.Deserialize<ConditionRule>(json, _options);

            return ConditionRule.IsEmpty(rule) ? null : rule;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes any value with the same options, such as the list of rule sources.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The JSON.</returns>
    public static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, _options);
}
