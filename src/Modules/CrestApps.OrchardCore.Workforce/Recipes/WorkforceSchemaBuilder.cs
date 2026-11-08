using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// Builds the schema fragments the Workforce recipe step schemas share. The Workforce steps read their records with the
/// web serializer defaults, so members are camel-cased (matched without regard to case) and enums are written by name.
/// </summary>
internal static class WorkforceSchemaBuilder
{
    /// <summary>
    /// Describes a string member.
    /// </summary>
    public static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    /// <summary>
    /// Describes a boolean member.
    /// </summary>
    public static JsonSchemaBuilder Flag(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);

    /// <summary>
    /// Describes an optional boolean member.
    /// </summary>
    public static JsonSchemaBuilder OptionalFlag(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description(description);

    /// <summary>
    /// Describes an integer member.
    /// </summary>
    public static JsonSchemaBuilder Integer(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description(description);

    /// <summary>
    /// Describes an optional integer member.
    /// </summary>
    public static JsonSchemaBuilder OptionalInteger(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description(description);

    /// <summary>
    /// Describes a number member.
    /// </summary>
    public static JsonSchemaBuilder Number(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number).Description(description);

    /// <summary>
    /// Describes an optional number member.
    /// </summary>
    public static JsonSchemaBuilder OptionalNumber(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description(description);

    /// <summary>
    /// Describes a date member, written as <c>yyyy-MM-dd</c>.
    /// </summary>
    public static JsonSchemaBuilder Date(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description(description);

    /// <summary>
    /// Describes an optional date member, written as <c>yyyy-MM-dd</c>.
    /// </summary>
    public static JsonSchemaBuilder OptionalDate(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Date).Description(description);

    /// <summary>
    /// Describes a time of day, written as <c>HH:mm</c> or <c>HH:mm:ss</c>.
    /// </summary>
    public static JsonSchemaBuilder Time(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern("^([01]\\d|2[0-3]):[0-5]\\d(:[0-5]\\d(\\.\\d+)?)?$").Description(description);

    /// <summary>
    /// Describes an optional time of day, written as <c>HH:mm</c> or <c>HH:mm:ss</c>.
    /// </summary>
    public static JsonSchemaBuilder OptionalTime(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Pattern("^([01]\\d|2[0-3]):[0-5]\\d(:[0-5]\\d(\\.\\d+)?)?$").Description(description);

    /// <summary>
    /// Describes an enum member written by name.
    /// </summary>
    public static JsonSchemaBuilder Enum<TEnum>(string description)
        where TEnum : struct, System.Enum
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(System.Enum.GetNames<TEnum>()).Description(description);

    /// <summary>
    /// Describes an optional enum member written by name.
    /// </summary>
    public static JsonSchemaBuilder OptionalEnum<TEnum>(string description)
        where TEnum : struct, System.Enum
        => new JsonSchemaBuilder()
            .AnyOf(
                new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(System.Enum.GetNames<TEnum>()),
                new JsonSchemaBuilder().Type(SchemaValueType.Null))
            .Description(description);

    /// <summary>
    /// Describes an array of enum values written by name.
    /// </summary>
    public static JsonSchemaBuilder EnumArray<TEnum>(string description)
        where TEnum : struct, System.Enum
        => Array(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(System.Enum.GetNames<TEnum>()), description);

    /// <summary>
    /// Describes an array of strings.
    /// </summary>
    public static JsonSchemaBuilder Strings(string description)
        => Array(new JsonSchemaBuilder().Type(SchemaValueType.String), description);

    /// <summary>
    /// Describes an array of dates written as <c>yyyy-MM-dd</c>.
    /// </summary>
    public static JsonSchemaBuilder Dates(string description)
        => Array(new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date), description);

    /// <summary>
    /// Describes an array.
    /// </summary>
    public static JsonSchemaBuilder Array(JsonSchemaBuilder items, string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(items).Description(description);

    /// <summary>
    /// Describes an object that may carry more members.
    /// </summary>
    public static JsonSchemaBuilder Object(string description, params (string Name, JsonSchemaBuilder Schema)[] properties)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description(description)
            .Properties(properties)
            .AdditionalProperties(true);

    /// <summary>
    /// Describes the identifier a record is matched by.
    /// </summary>
    public static JsonSchemaBuilder ItemId(string noun)
        => Text($"The {noun}'s id. When it matches a stored {noun}, that {noun} is updated; otherwise the {noun} is matched by name, and a new one keeps this id.");

    /// <summary>
    /// Describes a place: an address and optional coordinates.
    /// </summary>
    public static JsonSchemaBuilder Location(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object | SchemaValueType.Null)
            .Description(description)
            .Properties(
                ("address", Text("The address as one line.")),
                ("latitude", OptionalNumber("The latitude, in degrees.")),
                ("longitude", OptionalNumber("The longitude, in degrees.")))
            .AdditionalProperties(true);

    /// <summary>
    /// Builds a Workforce recipe step: the step name and its record arrays, all optional.
    /// </summary>
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
