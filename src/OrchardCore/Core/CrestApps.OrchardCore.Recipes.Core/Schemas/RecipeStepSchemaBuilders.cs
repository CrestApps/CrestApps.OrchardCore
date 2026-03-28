using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

internal static class RecipeStepSchemaBuilders
{
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

    public static JsonSchemaBuilder Boolean()
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean);

    public static JsonSchemaBuilder Integer()
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer);

    public static JsonSchemaBuilder Number()
        => new JsonSchemaBuilder().Type(SchemaValueType.Number);

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

    public static JsonSchemaBuilder String()
        => new JsonSchemaBuilder().Type(SchemaValueType.String);

    public static JsonSchemaBuilder StringArray(int? minItems = null)
        => Array(String(), minItems);
}
