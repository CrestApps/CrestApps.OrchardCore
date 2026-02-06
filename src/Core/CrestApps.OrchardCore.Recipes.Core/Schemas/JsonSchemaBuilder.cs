using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Fluent API for assembling a <see cref="JsonSchema"/>.
/// Collects keyword values into an internal bag, then materialises
/// a <see cref="JsonObject"/> when <see cref="Build"/> is called.
/// </summary>
public sealed class JsonSchemaBuilder
{
    // Maps JSON-Schema keyword names → the raw value to emit.
    // Values are one of: string, bool, int, JsonNode, List<…>, List<(…)>.
    private readonly Dictionary<string, object> _bag = new(StringComparer.Ordinal);

    // ── type ────────────────────────────────────────────────

    public JsonSchemaBuilder Type(SchemaValueType kind)
    {
        _bag["type"] = MapType(kind);
        return this;
    }

    // ── description ─────────────────────────────────────────

    public JsonSchemaBuilder Description(string text)
    {
        _bag["description"] = text;
        return this;
    }

    // ── const ───────────────────────────────────────────────

    public JsonSchemaBuilder Const(string value) { _bag["const"] = JsonValue.Create(value); return this; }

    public JsonSchemaBuilder Const(bool value) { _bag["const"] = JsonValue.Create(value); return this; }

    // ── enum ────────────────────────────────────────────────

    public JsonSchemaBuilder Enum(params string[] values) => AppendEnum(values);

    public JsonSchemaBuilder Enum(IEnumerable<string> values) => AppendEnum(values);

    private JsonSchemaBuilder AppendEnum(IEnumerable<string> values)
    {
        if (!_bag.TryGetValue("enum", out var existing))
        {
            existing = new List<JsonNode>();
            _bag["enum"] = existing;
        }

        var list = (List<JsonNode>)existing;
        foreach (var v in values)
        {
            list.Add(JsonValue.Create(v));
        }

        return this;
    }

    // ── pattern ─────────────────────────────────────────────

    public JsonSchemaBuilder Pattern(string regex)
    {
        _bag["pattern"] = regex;
        return this;
    }

    // ── default ─────────────────────────────────────────────

    public JsonSchemaBuilder Default(string value) { _bag["default"] = JsonValue.Create(value); return this; }

    public JsonSchemaBuilder Default(int value) { _bag["default"] = JsonValue.Create(value); return this; }

    public JsonSchemaBuilder Default(bool value) { _bag["default"] = JsonValue.Create(value); return this; }

    // ── properties ──────────────────────────────────────────

    public JsonSchemaBuilder Properties(params (string Key, JsonSchemaBuilder Sub)[] defs)
    {
        if (!_bag.TryGetValue("properties", out var existing))
        {
            existing = new List<(string, JsonSchemaBuilder)>();
            _bag["properties"] = existing;
        }

        ((List<(string, JsonSchemaBuilder)>)existing).AddRange(defs);
        return this;
    }

    // ── required ────────────────────────────────────────────

    public JsonSchemaBuilder Required(params string[] names)
    {
        if (!_bag.TryGetValue("required", out var existing))
        {
            existing = new List<string>();
            _bag["required"] = existing;
        }

        ((List<string>)existing).AddRange(names);
        return this;
    }

    // ── additionalProperties ────────────────────────────────

    public JsonSchemaBuilder AdditionalProperties(bool allowed)
    {
        _bag["additionalProperties"] = allowed;
        return this;
    }

    public JsonSchemaBuilder AdditionalProperties(JsonSchemaBuilder schema)
    {
        _bag["additionalProperties"] = schema;
        return this;
    }

    // ── minProperties ───────────────────────────────────────

    public JsonSchemaBuilder MinProperties(int min)
    {
        _bag["minProperties"] = min;
        return this;
    }

    // ── items / minItems ────────────────────────────────────

    public JsonSchemaBuilder Items(JsonSchemaBuilder itemSchema)
    {
        _bag["items"] = itemSchema;
        return this;
    }

    public JsonSchemaBuilder MinItems(int min)
    {
        _bag["minItems"] = min;
        return this;
    }

    // ── allOf / anyOf / oneOf ───────────────────────────────

    public JsonSchemaBuilder AllOf(IEnumerable<JsonSchema> schemas)
    {
        CollectSchemas("allOf", schemas);
        return this;
    }

    public JsonSchemaBuilder AnyOf(params JsonSchemaBuilder[] builders)
    {
        CollectBuilders("anyOf", builders);
        return this;
    }

    public JsonSchemaBuilder AnyOf(IEnumerable<JsonSchemaBuilder> builders)
    {
        CollectBuilders("anyOf", builders);
        return this;
    }

    public JsonSchemaBuilder OneOf(IEnumerable<JsonSchema> schemas)
    {
        CollectSchemas("oneOf", schemas);
        return this;
    }

    // ── if / then / else ────────────────────────────────────

    public JsonSchemaBuilder If(JsonSchemaBuilder condition) { _bag["if"] = condition; return this; }

    public JsonSchemaBuilder Then(JsonSchemaBuilder branch) { _bag["then"] = branch; return this; }

    public JsonSchemaBuilder Else(JsonSchemaBuilder branch) { _bag["else"] = branch; return this; }

    // ── Build ───────────────────────────────────────────────

    public JsonSchema Build()
    {
        var target = new JsonObject();

        foreach (var (keyword, raw) in _bag)
        {
            target[keyword] = Materialise(keyword, raw);
        }

        return new JsonSchema(target);
    }

    /// <summary>
    /// Implicit conversion so a builder can be used wherever a <see cref="JsonSchema"/> is expected.
    /// </summary>
    public static implicit operator JsonSchema(JsonSchemaBuilder b) => b.Build();

    // ── Private helpers ─────────────────────────────────────

    private void CollectSchemas(string keyword, IEnumerable<JsonSchema> schemas)
    {
        if (!_bag.TryGetValue(keyword, out var existing))
        {
            existing = new List<JsonSchema>();
            _bag[keyword] = existing;
        }

        ((List<JsonSchema>)existing).AddRange(schemas);
    }

    private void CollectBuilders(string keyword, IEnumerable<JsonSchemaBuilder> builders)
    {
        if (!_bag.TryGetValue(keyword, out var existing))
        {
            existing = new List<JsonSchema>();
            _bag[keyword] = existing;
        }

        var list = (List<JsonSchema>)existing;
        foreach (var b in builders)
        {
            list.Add(b.Build());
        }
    }

    private static JsonNode Materialise(string keyword, object raw) => keyword switch
    {
        "type" or "description" or "pattern" => JsonValue.Create((string)raw),
        "additionalProperties" => raw is bool b ? JsonValue.Create(b) : ((JsonSchemaBuilder)raw).Build().RawKeywords.DeepClone(),
        "minProperties" or "minItems" => JsonValue.Create((int)raw),
        "const" or "default" => ((JsonNode)raw).DeepClone(),

        "enum" => BuildJsonArray((List<JsonNode>)raw, static n => n.DeepClone()),

        "properties" => BuildPropertiesObject((List<(string, JsonSchemaBuilder)>)raw),

        "required" => BuildJsonArray((List<string>)raw, static s => JsonValue.Create(s)),

        "items" => ((JsonSchemaBuilder)raw).Build().RawKeywords.DeepClone(),

        "allOf" or "anyOf" or "oneOf" => BuildJsonArray(
            (List<JsonSchema>)raw, static s => s.RawKeywords.DeepClone()),

        "if" or "then" or "else" => ((JsonSchemaBuilder)raw).Build().RawKeywords.DeepClone(),

        _ => null,
    };

    private static JsonObject BuildPropertiesObject(List<(string Key, JsonSchemaBuilder Sub)> entries)
    {
        var obj = new JsonObject();
        foreach (var (key, sub) in entries)
        {
            obj[key] = sub.Build().RawKeywords.DeepClone();
        }

        return obj;
    }

    private static JsonArray BuildJsonArray<T>(List<T> source, Func<T, JsonNode> project)
    {
        var arr = new JsonArray();
        foreach (var item in source)
        {
            arr.Add(project(item));
        }

        return arr;
    }

    private static string MapType(SchemaValueType kind) => kind switch
    {
        SchemaValueType.Object => "object",
        SchemaValueType.Array => "array",
        SchemaValueType.String => "string",
        SchemaValueType.Boolean => "boolean",
        SchemaValueType.Number => "number",
        SchemaValueType.Integer => "integer",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
