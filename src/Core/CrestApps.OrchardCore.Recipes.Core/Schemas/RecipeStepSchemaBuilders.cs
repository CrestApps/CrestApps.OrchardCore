using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

internal static class RecipeStepSchemaBuilders
{
    /// <summary>
    /// Builds a JSON schema for a named recipe step with the specified properties and constraints.
    /// </summary>
    /// <param name="stepName">The name of the recipe step, added as a constant "name" property.</param>
    /// <param name="properties">The additional property definitions for the step schema.</param>
    /// <param name="requiredProperties">The names of properties that are required, in addition to "name".</param>
    /// <param name="additionalProperties">A value indicating whether the schema allows additional properties beyond those defined.</param>
    public static JsonSchema BuildNamedStep(
        string stepName,
        IEnumerable<(string Name, JsonSchemaBuilder Schema)> properties,
        IEnumerable<string> requiredProperties = null,
        bool additionalProperties = true)
    {
        var allProperties = new List<(string Name, JsonSchemaBuilder Schema)>
        {
            ("name", String().Const(stepName)),
        };

        allProperties.AddRange(properties);

        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(allProperties.ToDictionary(property => property.Name, property => property.Schema));

        var required = requiredProperties?.Prepend("name").ToArray() ?? ["name"];

        return builder
            .Required(required)
            .AdditionalProperties(additionalProperties)
            .Build();
    }

    /// <summary>
    /// Creates a JSON schema builder for an array type with the specified item schema.
    /// </summary>
    /// <param name="items">The schema builder for items in the array.</param>
    /// <param name="minItems">The optional minimum number of items in the array.</param>
    public static JsonSchemaBuilder Array(JsonSchemaBuilder items, int? minItems = null)
    {
        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(items);

        if (minItems.HasValue)
        {
            builder = builder.MinItems((uint)minItems.Value);
        }

        return builder;
    }

    /// <summary>
    /// Creates a JSON schema builder for a boolean type.
    /// </summary>
    public static JsonSchemaBuilder Boolean()
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean);

    /// <summary>
    /// Creates a JSON schema builder for an integer type.
    /// </summary>
    public static JsonSchemaBuilder Integer()
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer);

    /// <summary>
    /// Creates a JSON schema builder for a number type.
    /// </summary>
    public static JsonSchemaBuilder Number()
        => new JsonSchemaBuilder().Type(SchemaValueType.Number);

    /// <summary>
    /// Creates a JSON schema builder for the null type.
    /// </summary>
    public static JsonSchemaBuilder Null()
        => new JsonSchemaBuilder().Type(SchemaValueType.Null);

    /// <summary>
    /// Creates a JSON schema builder for an object type with optional properties and constraints.
    /// </summary>
    /// <param name="properties">The optional property definitions for the object schema.</param>
    /// <param name="requiredProperties">The optional names of properties that are required.</param>
    /// <param name="additionalProperties">A value indicating whether the schema allows additional properties beyond those defined.</param>
    public static JsonSchemaBuilder Object(
        IEnumerable<(string Name, JsonSchemaBuilder Schema)> properties = null,
        IEnumerable<string> requiredProperties = null,
        bool additionalProperties = true)
    {
        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object);

        if (properties is not null)
        {
            builder = builder.Properties(properties.ToDictionary(property => property.Name, property => property.Schema));
        }

        if (requiredProperties is not null)
        {
            builder = builder.Required(requiredProperties.ToArray());
        }

        return builder.AdditionalProperties(additionalProperties);
    }

    /// <summary>
    /// Creates a JSON schema builder for a string type.
    /// </summary>
    public static JsonSchemaBuilder String()
        => new JsonSchemaBuilder().Type(SchemaValueType.String);

    /// <summary>
    /// Creates a schema builder that allows the provided schema or a null value.
    /// </summary>
    /// <param name="schema">The schema to make nullable.</param>
    public static JsonSchemaBuilder Nullable(JsonSchemaBuilder schema)
        => new JsonSchemaBuilder().AnyOf(schema, Null());

    /// <summary>
    /// Creates a JSON schema builder for an array of strings.
    /// </summary>
    /// <param name="minItems">The optional minimum number of items in the array.</param>
    public static JsonSchemaBuilder StringArray(int? minItems = null)
        => Array(String(), minItems);
}
