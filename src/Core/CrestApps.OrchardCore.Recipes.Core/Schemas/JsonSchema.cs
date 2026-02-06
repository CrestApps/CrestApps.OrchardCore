using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// An immutable JSON Schema that can validate JSON documents and serialize to standard format.
/// Internally backed by a <see cref="JsonObject"/> representing the raw schema keywords.
/// </summary>
[JsonConverter(typeof(SchemaSerializer))]
public sealed class JsonSchema
{
    internal JsonObject RawKeywords { get; }

    internal JsonSchema(JsonObject keywords)
    {
        RawKeywords = keywords ?? new JsonObject();
    }

    /// <summary>
    /// Checks whether <paramref name="document"/> conforms to this schema.
    /// </summary>
    public EvaluationResult Evaluate(JsonNode document, EvaluationOptions options = null)
        => new(RunChecks(RawKeywords, document));

    public override string ToString()
        => RawKeywords?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{}";

    // ── Constraint pipeline ─────────────────────────────────
    // Each method returns true when the constraint is satisfied (or absent).

    private static bool RunChecks(JsonObject kw, JsonNode node)
    {
        if (kw is null || kw.Count == 0)
        {
            return true;
        }

        return CheckTypeKeyword(kw, node)
            && CheckConstKeyword(kw, node)
            && CheckEnumKeyword(kw, node)
            && CheckPatternKeyword(kw, node)
            && CheckObjectKeywords(kw, node)
            && CheckArrayKeywords(kw, node)
            && CheckCompositionKeywords(kw, node)
            && CheckConditionalKeywords(kw, node);
    }

    private static bool CheckTypeKeyword(JsonObject kw, JsonNode node)
    {
        if (!TryReadString(kw, "type", out var expected))
        {
            return true;
        }

        return expected switch
        {
            "object" => node is JsonObject,
            "array" => node is JsonArray,
            "string" => IsValueKind(node, JsonValueKind.String),
            "boolean" => IsValueKind(node, JsonValueKind.True) || IsValueKind(node, JsonValueKind.False),
            "number" or "integer" => IsValueKind(node, JsonValueKind.Number),
            _ => true,
        };
    }

    private static bool CheckConstKeyword(JsonObject kw, JsonNode node)
    {
        if (!kw.TryGetPropertyValue("const", out var expected))
        {
            return true;
        }

        return JsonNode.DeepEquals(node, expected);
    }

    private static bool CheckEnumKeyword(JsonObject kw, JsonNode node)
    {
        if (!kw.TryGetPropertyValue("enum", out var raw) || raw is not JsonArray allowedValues)
        {
            return true;
        }

        foreach (var candidate in allowedValues)
        {
            if (JsonNode.DeepEquals(node, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CheckPatternKeyword(JsonObject kw, JsonNode node)
    {
        if (!TryReadString(kw, "pattern", out var regex))
        {
            return true;
        }

        var subject = IsValueKind(node, JsonValueKind.String)
            ? ((JsonValue)node).GetValue<string>()
            : node?.ToJsonString();

        return subject is not null && Regex.IsMatch(subject, regex);
    }

    private static bool CheckObjectKeywords(JsonObject kw, JsonNode node)
    {
        if (node is not JsonObject objectNode)
        {
            return true; // non-objects pass object-specific constraints vacuously
        }

        // "required"
        if (kw.TryGetPropertyValue("required", out var rNode) && rNode is JsonArray requiredArr)
        {
            foreach (var entry in requiredArr)
            {
                if (entry is not null && !objectNode.ContainsKey(entry.GetValue<string>()))
                {
                    return false;
                }
            }
        }

        // "properties"
        JsonObject declaredProps = null;
        if (kw.TryGetPropertyValue("properties", out var pNode) && pNode is JsonObject propsMap)
        {
            declaredProps = propsMap;
            foreach (var pair in propsMap)
            {
                if (objectNode.TryGetPropertyValue(pair.Key, out var childVal) &&
                    pair.Value is JsonObject childKw &&
                    !RunChecks(childKw, childVal))
                {
                    return false;
                }
            }
        }

        // "additionalProperties": false
        if (declaredProps is not null &&
            kw.TryGetPropertyValue("additionalProperties", out var apNode) &&
            apNode is JsonValue apVal && IsBoolFalse(apVal))
        {
            foreach (var pair in objectNode)
            {
                if (!declaredProps.ContainsKey(pair.Key))
                {
                    return false;
                }
            }
        }

        // "minProperties"
        if (TryReadInt(kw, "minProperties", out var mp) && objectNode.Count < mp)
        {
            return false;
        }

        return true;
    }

    private static bool CheckArrayKeywords(JsonObject kw, JsonNode node)
    {
        if (node is not JsonArray arrayNode)
        {
            return true;
        }

        // "minItems"
        if (TryReadInt(kw, "minItems", out var mi) && arrayNode.Count < mi)
        {
            return false;
        }

        // "items"
        if (kw.TryGetPropertyValue("items", out var iNode) && iNode is JsonObject itemKw)
        {
            foreach (var element in arrayNode)
            {
                if (!RunChecks(itemKw, element))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool CheckCompositionKeywords(JsonObject kw, JsonNode node)
    {
        // allOf – every sub-schema must pass
        if (kw.TryGetPropertyValue("allOf", out var aNode) && aNode is JsonArray allArr)
        {
            foreach (var sub in allArr)
            {
                if (sub is JsonObject s && !RunChecks(s, node))
                {
                    return false;
                }
            }
        }

        // anyOf – at least one must pass
        if (kw.TryGetPropertyValue("anyOf", out var yNode) && yNode is JsonArray anyArr)
        {
            var matched = false;
            foreach (var sub in anyArr)
            {
                if (sub is JsonObject s && RunChecks(s, node))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                return false;
            }
        }

        // oneOf – exactly one must pass
        if (kw.TryGetPropertyValue("oneOf", out var oNode) && oNode is JsonArray oneArr)
        {
            var passCount = 0;
            foreach (var sub in oneArr)
            {
                if (sub is JsonObject s && RunChecks(s, node) && ++passCount > 1)
                {
                    break;
                }
            }

            if (passCount != 1)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CheckConditionalKeywords(JsonObject kw, JsonNode node)
    {
        if (!kw.TryGetPropertyValue("if", out var ifRaw) || ifRaw is not JsonObject ifKw)
        {
            return true;
        }

        if (RunChecks(ifKw, node))
        {
            return !kw.TryGetPropertyValue("then", out var tRaw) ||
                   tRaw is not JsonObject thenKw ||
                   RunChecks(thenKw, node);
        }

        return !kw.TryGetPropertyValue("else", out var eRaw) ||
               eRaw is not JsonObject elseKw ||
               RunChecks(elseKw, node);
    }

    // ── Utility helpers ─────────────────────────────────────

    private static bool IsValueKind(JsonNode node, JsonValueKind kind)
        => node is JsonValue jv && jv.GetValueKind() == kind;

    private static bool IsBoolFalse(JsonValue val)
        => val.GetValueKind() is JsonValueKind.False;

    private static bool TryReadString(JsonObject obj, string key, out string result)
    {
        result = null;
        if (obj.TryGetPropertyValue(key, out var n) && n is JsonValue jv && jv.GetValueKind() == JsonValueKind.String)
        {
            result = jv.GetValue<string>();
            return true;
        }

        return false;
    }

    private static bool TryReadInt(JsonObject obj, string key, out int result)
    {
        result = 0;
        if (obj.TryGetPropertyValue(key, out var n) && n is JsonValue jv && jv.GetValueKind() == JsonValueKind.Number)
        {
            result = jv.GetValue<int>();
            return true;
        }

        return false;
    }

    // ── Serialization ───────────────────────────────────────

    private sealed class SchemaSerializer : JsonConverter<JsonSchema>
    {
        public override JsonSchema Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(JsonSerializer.Deserialize<JsonObject>(ref reader, options));

        public override void Write(Utf8JsonWriter writer, JsonSchema value, JsonSerializerOptions options)
        {
            if (value?.RawKeywords is not null)
            {
                value.RawKeywords.WriteTo(writer, options);
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
        }
    }
}
