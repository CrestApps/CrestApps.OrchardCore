using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

/// <summary>
/// Walks a model type and the JSON schema that describes it side by side, reporting every member the schema does not
/// describe faithfully.
/// </summary>
/// <remarks>
/// A recipe step schema is hand-written, while the record it describes grows a property whenever a feature needs one.
/// Nothing ties the two together, so a schema quietly falls behind: the import still accepts the new property, because
/// the schemas allow additional properties, but an author or an AI agent reading the schema never learns it exists, and
/// a value the schema declares with the wrong type or an out-of-date list of enum names is refused by validation. The
/// walk is driven by the type rather than by a sample document so that a member left at its default is still checked.
/// </remarks>
internal static class ContactCenterRecipeSchemaCoverage
{
    /// <summary>
    /// The members the exporting environment owns and never writes into a plan.
    /// </summary>
    public static readonly IReadOnlySet<string> EnvironmentOwnedMembers = new HashSet<string>(StringComparer.Ordinal)
    {
        "CreatedUtc",
        "ModifiedUtc",
        "OwnerId",
        "Author",
    };

    /// <summary>
    /// Reports the members of <paramref name="type"/> that <paramref name="schema"/> omits or describes incorrectly.
    /// </summary>
    /// <param name="type">The model type the schema describes.</param>
    /// <param name="schema">The JSON form of the object schema.</param>
    /// <param name="path">The path used to name a problem.</param>
    /// <param name="problems">Receives one line per problem.</param>
    /// <param name="ignored">Top-level members that are deliberately not described.</param>
    /// <param name="only">When supplied, only these top-level members are checked.</param>
    public static void CheckObject(
        Type type,
        JsonObject schema,
        string path,
        List<string> problems,
        IEnumerable<string> ignored = null,
        IEnumerable<string> only = null)
    {
        var ignoredSet = new HashSet<string>(ignored ?? [], StringComparer.Ordinal);
        var onlySet = only is null ? null : new HashSet<string>(only, StringComparer.Ordinal);

        CheckObject(type, schema, path, problems, ignoredSet, onlySet, depth: 0);
    }

    private static void CheckObject(
        Type type,
        JsonObject schema,
        string path,
        List<string> problems,
        HashSet<string> ignored,
        HashSet<string> only,
        int depth)
    {
        if (schema["properties"] is not JsonObject properties)
        {
            problems.Add($"{path}: the schema declares no properties for {type.Name}.");

            return;
        }

        var required = (schema["required"] as JsonArray)?
            .Select(node => node?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal) ?? [];

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead
                || !property.CanWrite
                || property.GetIndexParameters().Length > 0
                || property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }

            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;

            if ((depth == 0 && (ignored.Contains(name) || EnvironmentOwnedMembers.Contains(name)))
                || (depth == 0 && only is not null && !only.Contains(name)))
            {
                continue;
            }

            if (properties[name] is not JsonObject propertySchema)
            {
                problems.Add($"{path}.{name}: {type.Name}.{property.Name} is not described by the schema.");

                continue;
            }

            if (propertySchema["description"] is null)
            {
                problems.Add($"{path}.{name}: the property has no description.");
            }

            CheckValue(property.PropertyType, propertySchema, $"{path}.{name}", problems, required.Contains(name), depth);
        }
    }

    private static void CheckValue(Type type, JsonObject schema, string path, List<string> problems, bool isRequired, int depth)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        var types = ReadTypes(schema);

        if (types.Count == 0)
        {
            // A composed schema (anyOf, oneOf) describes the value by its branches, which the walk does not follow.
            if (schema["anyOf"] is null && schema["oneOf"] is null && schema["$ref"] is null)
            {
                problems.Add($"{path}: the schema declares no type.");
            }

            return;
        }

        var canBeNull = !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
        var isCollection = underlying != typeof(string) && typeof(IEnumerable).IsAssignableFrom(underlying);

        // An export writes a null member rather than omitting it, so a nullable member the schema refuses null for makes
        // the exported plan invalid against its own schema. Collections are initialized and a required member is
        // enforced by the import, so neither is expected to travel as null.
        if (canBeNull && !isCollection && !isRequired && !types.Contains("null"))
        {
            problems.Add($"{path}: the member can be null but the schema does not allow null.");
        }

        var expected = GetExpectedType(underlying);

        if (expected is not null && !types.Contains(expected))
        {
            problems.Add($"{path}: expected schema type '{expected}' but found '{string.Join("|", types)}'.");
        }

        if (underlying.IsEnum)
        {
            var declared = (schema["enum"] as JsonArray)?
                .Where(node => node is not null)
                .Select(node => node.GetValue<string>())
                .Order(StringComparer.Ordinal)
                .ToArray() ?? [];
            var actual = Enum.GetNames(underlying).Order(StringComparer.Ordinal).ToArray();

            if (!declared.SequenceEqual(actual, StringComparer.Ordinal))
            {
                problems.Add($"{path}: the enum lists [{string.Join(", ", declared)}] but {underlying.Name} has [{string.Join(", ", actual)}].");
            }

            return;
        }

        if (depth >= 4 || IsDictionary(underlying))
        {
            return;
        }

        if (isCollection)
        {
            var elementType = GetElementType(underlying);

            if (elementType is not null && schema["items"] is JsonObject itemSchema)
            {
                CheckValue(elementType, itemSchema, $"{path}[]", problems, isRequired: true, depth + 1);
            }
            else if (elementType is not null)
            {
                problems.Add($"{path}: the array schema does not describe its items.");
            }

            return;
        }

        if (IsComplex(underlying))
        {
            CheckObject(underlying, schema, path, problems, [], null, depth + 1);
        }
    }

    private static HashSet<string> ReadTypes(JsonObject schema)
    {
        return schema["type"] switch
        {
            JsonValue value => [value.GetValue<string>()],
            JsonArray array => array.Select(node => node.GetValue<string>()).ToHashSet(StringComparer.Ordinal),
            _ => [],
        };
    }

    private static string GetExpectedType(Type type)
    {
        if (type == typeof(string) || type.IsEnum || type == typeof(DateTime) || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan) || type == typeof(TimeOnly) || type == typeof(DateOnly) || type == typeof(Guid))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
        {
            return "integer";
        }

        if (type == typeof(double) || type == typeof(decimal) || type == typeof(float))
        {
            return "number";
        }

        if (IsDictionary(type) || IsComplex(type))
        {
            return "object";
        }

        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            return "array";
        }

        return null;
    }

    private static bool IsComplex(Type type)
        => type.IsClass && type != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(type) && !typeof(JsonNode).IsAssignableFrom(type);

    private static bool IsDictionary(Type type)
        => typeof(IDictionary).IsAssignableFrom(type)
            || typeof(JsonObject).IsAssignableFrom(type)
            || type.GetInterfaces().Append(type).Any(candidate => candidate.IsGenericType
                && (candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>) || candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

    private static Type GetElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        return type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))?
            .GetGenericArguments()[0];
    }
}
