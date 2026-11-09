using Json.Schema;

namespace CrestApps.OrchardCore.Assets.Recipes;

/// <summary>
/// Builds the schema fragments of the Assets recipe step. Its records are read with Orchard Core's default
/// serializer options and only the members present in an entry are applied, so members are written in PascalCase,
/// exactly as a deployment plan exports them, and enums are written by name.
/// </summary>
internal static class AssetsSchemaBuilder
{
    public static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    public static JsonSchemaBuilder Flag(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);

    public static JsonSchemaBuilder Integer(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description(description);

    public static JsonSchemaBuilder Number(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number).Description(description);

    public static JsonSchemaBuilder OptionalNumber(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description(description);

    public static JsonSchemaBuilder Date(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description(description);

    public static JsonSchemaBuilder OptionalDate(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Date).Description(description);

    public static JsonSchemaBuilder Time(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern("^([01][0-9]|2[0-3]):[0-5][0-9](:[0-5][0-9]([.][0-9]+)?)?$").Description(description);

    public static JsonSchemaBuilder Enum<TEnum>(string description)
        where TEnum : struct, System.Enum
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(System.Enum.GetNames<TEnum>()).Description(description);

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

    public static JsonSchemaBuilder OptionalObject(string description, params (string Name, JsonSchemaBuilder Schema)[] properties)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object | SchemaValueType.Null)
            .Description(description)
            .Properties(properties)
            .AdditionalProperties(true);

    public static JsonSchemaBuilder ItemId(string noun)
        => Text($"The {noun}'s id. When it matches a stored {noun}, that {noun} is updated with the members given here; otherwise a new {noun} is created, keeping this id when it is a valid id so other records that point at it still do.");

    public static JsonSchemaBuilder Address(string description)
        => OptionalObject(
            description,
            ("Name", Text("The name of the person or place.")),
            ("Company", Text("The company name.")),
            ("AddressLine1", Text("The first street line.")),
            ("AddressLine2", Text("The second street line.")),
            ("DependentLocality", Text("The neighborhood or dependent locality, where the country uses one.")),
            ("SortingCode", Text("The sorting code, where the country uses one.")),
            ("Country", Text("The two-letter ISO country code.")),
            ("Region", Text("The state, province or region.")),
            ("County", Text("The county.")),
            ("City", Text("The city.")),
            ("District", Text("The district.")),
            ("PostalCode", Text("The postal code.")),
            ("Phone", Text("The phone number at the address.")),
            ("Latitude", OptionalNumber("The latitude, in degrees, when geocoded.")),
            ("Longitude", OptionalNumber("The longitude, in degrees, when geocoded.")));

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
