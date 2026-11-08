using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Recipes;

/// <summary>
/// Builds the schema fragments of the Workforce contact center recipe steps. These steps read their records with the
/// web serializer defaults, so members are camel-cased (matched without regard to case) and enums are written by name.
/// </summary>
internal static class WorkforceContactCenterSchemaBuilder
{
    public static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    public static JsonSchemaBuilder Flag(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);

    public static JsonSchemaBuilder Integer(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description(description);

    public static JsonSchemaBuilder OptionalInteger(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description(description);

    public static JsonSchemaBuilder Number(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number).Description(description);

    public static JsonSchemaBuilder OptionalNumber(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description(description);

    public static JsonSchemaBuilder Date(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description(description);

    public static JsonSchemaBuilder Enum<TEnum>(string description)
        where TEnum : struct, System.Enum
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(System.Enum.GetNames<TEnum>()).Description(description);

    public static JsonSchemaBuilder OptionalEnum<TEnum>(string description)
        where TEnum : struct, System.Enum
        => new JsonSchemaBuilder()
            .AnyOf(
                new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(System.Enum.GetNames<TEnum>()),
                new JsonSchemaBuilder().Type(SchemaValueType.Null))
            .Description(description);

    public static JsonSchemaBuilder EnumArray<TEnum>(string description)
        where TEnum : struct, System.Enum
        => Array(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(System.Enum.GetNames<TEnum>()), description);

    public static JsonSchemaBuilder Strings(string description)
        => Array(new JsonSchemaBuilder().Type(SchemaValueType.String), description);

    public static JsonSchemaBuilder Array(JsonSchemaBuilder items, string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(items).Description(description);

    public static JsonSchemaBuilder Object(string description, params (string Name, JsonSchemaBuilder Schema)[] properties)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description(description)
            .Properties(properties)
            .AdditionalProperties(true);

    public static JsonSchemaBuilder ItemId(string noun)
        => Text($"The {noun}'s id. When it matches a stored {noun}, that {noun} is updated; otherwise the {noun} is matched by name, and a new one keeps this id.");

    public static JsonSchema Step(string name, string description, params (string Name, JsonSchemaBuilder Schema)[] sections)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description(description)
            .Properties(
            [
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(name).Description($"Recipe step discriminator. Must be '{name}'.")),
                .. sections,
            ])
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
