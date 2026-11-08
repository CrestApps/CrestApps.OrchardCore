using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Commerce.StoreProfile.Recipes;

/// <summary>
/// Describes the <c>StoreProfileSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class StoreProfileSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(StoreProfileSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Who the store is (Settings › Commerce › Store): its name, logo, tax ID, how to reach it and where it is. Receipts, the address parcels ship from and the tax origin fall back to it when their own settings are empty.")
            .Properties(
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The store's name, such as the business's legal or trading name; the site's name when empty.")),
                ("LogoUrl", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The address of the store's logo, such as a media library path or an absolute URL.")),
                ("TaxId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The store's tax ID, such as a VAT, GST or EIN number.")),
                ("Email", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The e-mail address customers reach the store at.")),
                ("Phone", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The phone number customers reach the store at, in E.164 form when it parses.")),
                ("Address", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Properties(
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
                    ("GeocodeSource", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Where the coordinates came from: a geocoding provider's name, or Manual when pinned."))).AdditionalProperties(true).Description("The store's address, or null.")))
            .AdditionalProperties(true);
}
