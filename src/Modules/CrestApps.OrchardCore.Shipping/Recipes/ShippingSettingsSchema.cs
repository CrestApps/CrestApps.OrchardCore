using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Shipping.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Shipping.Recipes;

/// <summary>
/// Describes the <c>ShippingSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ShippingSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(ShippingSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Settings › Commerce › Shipping.")
            .Properties(
                ("WeightUnit", new JsonSchemaBuilder().AnyOf(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Kilogram", "Pound"), new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Default("Kilogram").Description("The unit product weights and weight tables are in.")),
                ("ShippingClasses", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The shipping classes products can be in, such as Bulky, which methods can charge extra for.")),
                ("Carriers", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(
                    ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The carrier's name, such as UPS.")),
                    ("TrackingUrlTemplate", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The tracking link, with {0} where the tracking number goes."))).AdditionalProperties(true)).Description("The carriers shipments are sent with, each with the link its parcels are tracked at.")),
                ("SendShippedEmails", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether the buyer gets an email, with the tracking link, when a shipment is marked shipped.")),
                ("PickupLocations", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(
                    ("LocationId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The location's identifier, which orders refer to.")),
                    ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The name buyers see, such as Downtown store.")),
                    ("AddressLine1", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The street address of the location.")),
                    ("City", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The city of the location.")),
                    ("Region", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The state, province or region of the location.")),
                    ("PostalCode", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The postal code of the location.")),
                    ("Country", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The two-letter country code.")),
                    ("Instructions", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the buyer is told, such as opening hours or \"ready in 2 hours\".")),
                    ("Fee", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("What picking up from the location costs; zero for free.")),
                    ("StockLocationId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The inventory location the order's units come from, or null to let inventory choose."))).AdditionalProperties(true)).Description("The store's locations buyers can pick their order up from, instead of having it shipped.")),
                ("DefaultParcelLength", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The size of the box most orders go in, in centimeters for a store weighing in kilograms and inches otherwise, which carriers quote with.")),
                ("DefaultParcelWidth", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The width of the default box, in the same unit as DefaultParcelLength.")),
                ("DefaultParcelHeight", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The height of the default box, in the same unit as DefaultParcelLength.")),
                ("PackagingWeight", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The weight of the box and packing, added to the items' weight, in the store's weight unit.")))
            .AdditionalProperties(true);
}
