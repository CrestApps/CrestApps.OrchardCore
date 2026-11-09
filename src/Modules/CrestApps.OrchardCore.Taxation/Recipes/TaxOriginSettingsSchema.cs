using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Taxation.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Describes the <c>TaxOriginSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TaxOriginSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(TaxOriginSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Where the store sells from. Taxes sourced at the origin, such as some states' sales tax, use it when an item doesn't say where it ships from.")
            .Properties(
                ("Origin", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Properties(
                    ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The recipient full name (the person or attention line the address is addressed to).")),
                    ("Company", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The company or organization name, when the address belongs to a business.")),
                    ("AddressLine1", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The first street line (street number and name, or PO box).")),
                    ("AddressLine2", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The second street line (apartment, suite, unit, or building), when present.")),
                    ("DependentLocality", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The part of the city the address is in, for the countries whose post uses one: a suburb, a neighbourhood (a Brazilian bairro), or a Chinese or Korean district.")),
                    ("SortingCode", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The sorting code a few countries add after the city, such as the French CEDEX.")),
                    ("Country", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The ISO country code (for example US or CA).")),
                    ("Region", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The state, province or region. For a country with a list of regions it is the region's code: its ISO 3166-2 code without the country (CA for California, 13 for Tokyo), or the key the country's post uses when there is no ISO code.")),
                    ("County", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The county name or code.")),
                    ("City", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The city name.")),
                    ("District", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The special or tax district identifier.")),
                    ("PostalCode", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The postal or ZIP code.")),
                    ("Phone", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The contact phone number for the address, used for delivery and fulfillment.")),
                    ("Latitude", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The latitude of the address, when it was geocoded or pinned on a map.")),
                    ("Longitude", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The longitude of the address, when it was geocoded or pinned on a map.")),
                    ("GeocodedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("When the address was geocoded.")),
                    ("GeocodeSource", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Where the coordinates came from: a geocoding provider's name, or Manual when pinned."))).AdditionalProperties(true).Description("The store's address, or null when it isn't set.")))
            .AdditionalProperties(true);
}
