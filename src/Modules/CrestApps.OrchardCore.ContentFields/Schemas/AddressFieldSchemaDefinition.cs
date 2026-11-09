using CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;
using Json.Schema;

namespace CrestApps.OrchardCore.ContentFields.Schemas;

internal sealed class AddressFieldSchemaDefinition : FieldSchemaDefinitionBase
{
    public override string Name => "AddressField";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("AddressFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The hint shown under the field in the editor.")),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an address must be entered.")),
                        ("ShowName", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether the editor asks for the recipient's name.")),
                        ("ShowCompany", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether the editor asks for a company.")),
                        ("ShowPhone", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the editor asks for a phone number.")),
                        ("Countries", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array, SchemaValueType.Null)
                            .Items(CountryCode())
                            .Description("The ISO 3166-1 alpha-2 codes of the countries offered; empty or null for every country.")),
                        ("DefaultCountry", CountryCode()
                            .Description("The country a new address starts in.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Address", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object, SchemaValueType.Null)
                    .Description("The address. Region is the region's ISO 3166-2 code without the country when the country lists regions.")
                    .Properties(
                        ("Name", Text()),
                        ("Company", Text()),
                        ("AddressLine1", Text()),
                        ("AddressLine2", Text()),
                        ("DependentLocality", Text()),
                        ("City", Text()),
                        ("Region", Text()),
                        ("County", Text()),
                        ("District", Text()),
                        ("PostalCode", Text()),
                        ("SortingCode", Text()),
                        ("Country", CountryCode()),
                        ("Phone", Text()),
                        ("Latitude", new JsonSchemaBuilder().Type(SchemaValueType.Number, SchemaValueType.Null).Minimum(-90).Maximum(90).Description("The latitude found by geocoding the address.")),
                        ("Longitude", new JsonSchemaBuilder().Type(SchemaValueType.Number, SchemaValueType.Null).Minimum(-180).Maximum(180).Description("The longitude found by geocoding the address.")),
                        ("GeocodedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("When the address was geocoded, in UTC.")),
                        ("GeocodeSource", Text().Description("The geocoding provider that found the coordinates.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder Text()
        => new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null);

    private static JsonSchemaBuilder CountryCode()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String, SchemaValueType.Null)
            .Pattern("^[A-Za-z]{2}$");
}
