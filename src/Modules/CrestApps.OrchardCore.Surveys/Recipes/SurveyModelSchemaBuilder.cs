using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Parties.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Builds the JSON schema of a Surveys model from its public properties, so the schema follows the model as it grows.
/// Conditions are described with the shared rule schema; a question's settings with the schema of every registered
/// question type.
/// </summary>
internal static class SurveyModelSchemaBuilder
{
    /// <summary>
    /// Builds the schema of a type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="settingsSchemas">The settings schemas of the registered question types, used for the Settings of a
    /// question; empty to describe Settings as any object.</param>
    /// <param name="exclude">Property names to leave out.</param>
    /// <returns>The schema.</returns>
    public static JsonSchemaBuilder Build(Type type, IReadOnlyList<JsonSchemaBuilder> settingsSchemas, params string[] exclude)
        => BuildObject(type, settingsSchemas ?? [], new HashSet<Type>(), exclude);

    /// <summary>
    /// Builds the schema of a settings type from its public properties.
    /// </summary>
    /// <param name="settingsType">The settings type.</param>
    /// <returns>The schema, as JSON.</returns>
    public static JsonObject BuildSettings(Type settingsType)
    {
        if (settingsType is null)
        {
            return new JsonObject { ["type"] = "object" };
        }

        var schema = BuildObject(settingsType, [], new HashSet<Type>(), []).Build();

        return JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(schema))?.AsObject() ?? [];
    }

    private static JsonSchemaBuilder BuildObject(Type type, IReadOnlyList<JsonSchemaBuilder> settingsSchemas, HashSet<Type> visiting, string[] exclude)
    {
        var properties = new Dictionary<string, JsonSchemaBuilder>(StringComparer.Ordinal);

        if (visiting.Add(type))
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0
                    || property.GetCustomAttribute<JsonIgnoreAttribute>() is not null
                    || exclude.Contains(property.Name, StringComparer.Ordinal))
                {
                    continue;
                }

                properties[property.Name] = Describe(property.Name, property.PropertyType, settingsSchemas, visiting);
            }

            visiting.Remove(type);
        }

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object | SchemaValueType.Null)
            .Properties(properties)
            .AdditionalProperties(true);
    }

    private static JsonSchemaBuilder Describe(string name, Type type, IReadOnlyList<JsonSchemaBuilder> settingsSchemas, HashSet<Type> visiting)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(ConditionRule))
        {
            return ConditionRuleSchema.Build("When it applies.", "A question id, question-id.row-id, var:name, score, score:category or context:key.");
        }

        if (underlying == typeof(JsonObject))
        {
            if (name == "Settings" && settingsSchemas.Count > 0)
            {
                return new JsonSchemaBuilder()
                    .Description("The settings of the question type, as its type describes them.")
                    .AnyOf(settingsSchemas);
            }

            return new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).AdditionalProperties(true);
        }

        if (underlying == typeof(string))
        {
            return new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null);
        }

        if (underlying == typeof(bool))
        {
            return new JsonSchemaBuilder().Type(SchemaValueType.Boolean);
        }

        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short))
        {
            return new JsonSchemaBuilder().Type(Nullable.GetUnderlyingType(type) is null ? SchemaValueType.Integer : SchemaValueType.Integer | SchemaValueType.Null);
        }

        if (underlying == typeof(decimal) || underlying == typeof(double) || underlying == typeof(float))
        {
            return new JsonSchemaBuilder().Type(Nullable.GetUnderlyingType(type) is null ? SchemaValueType.Number : SchemaValueType.Number | SchemaValueType.Null);
        }

        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
        {
            return new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("An ISO 8601 date and time in UTC.");
        }

        if (underlying == typeof(DateOnly))
        {
            return new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Date);
        }

        if (underlying == typeof(TimeOnly))
        {
            return new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("A time of day, HH:mm:ss.");
        }

        if (underlying == typeof(RecordRef) || underlying == typeof(PartyRef))
        {
            return new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("A reference written as Kind:Id.");
        }

        if (underlying.IsEnum)
        {
            return new JsonSchemaBuilder()
                .Type(SchemaValueType.String | SchemaValueType.Integer)
                .Description("One of: " + string.Join(", ", Enum.GetNames(underlying)) + (underlying.GetCustomAttribute<FlagsAttribute>() is null ? "." : ", or several of them separated by commas."));
        }

        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var valueType = underlying.GetGenericArguments()[1];

            return new JsonSchemaBuilder()
                .Type(SchemaValueType.Object | SchemaValueType.Null)
                .AdditionalProperties(Describe(name, valueType, settingsSchemas, visiting));
        }

        if (underlying != typeof(string) && typeof(IEnumerable).IsAssignableFrom(underlying))
        {
            var itemType = underlying.IsArray ? underlying.GetElementType() : underlying.GetGenericArguments().FirstOrDefault() ?? typeof(object);

            return new JsonSchemaBuilder()
                .Type(SchemaValueType.Array | SchemaValueType.Null)
                .Items(Describe(name, itemType, settingsSchemas, visiting));
        }

        if (underlying.IsClass && underlying != typeof(object))
        {
            return BuildObject(underlying, settingsSchemas, visiting, []);
        }

        return new JsonSchemaBuilder();
    }
}
