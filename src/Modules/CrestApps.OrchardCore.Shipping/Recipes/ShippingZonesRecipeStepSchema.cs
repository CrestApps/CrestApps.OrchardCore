using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Shipping.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Shipping.Recipes;

/// <summary>
/// Describes the <c>ShippingZones</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ShippingZonesRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => ShippingConstants.ZonesRecipeStep;

    protected override string EntriesName => ShippingConstants.ZonesRecipeStepEntries;

    protected override string Description => "Creates or updates shipping zones, matched by ItemId, then by name. Only the members given are changed.";

    protected override string EntryDescription => "A shipping zone: the countries and regions it covers and the shipping methods it offers there.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
    {
        var method = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("A way to ship the zone offers, priced by a rate provider.")
            .Properties(
                ("MethodId", Nullable(SchemaValueType.String, "The method's identifier, which orders refer to. Generated when omitted.")),
                ("Name", Nullable(SchemaValueType.String, "The name buyers see, such as Standard.")),
                ("Description", Nullable(SchemaValueType.String, "What buyers see under the name, such as 3 to 5 business days.")),
                ("ProviderName", RecipeStepSchemaBuilders.SuggestedString(
                [
                    ShippingConstants.Providers.Flat,
                    ShippingConstants.Providers.Table,
                    ShippingConstants.Providers.Free,
                    ShippingConstants.Providers.LocalDelivery,
                    ShippingConstants.Providers.Carrier,
                ]).Description("The rate provider that prices the method. Other modules can register more.")),
                ("Enabled", RecipeStepSchemaBuilders.Boolean().Description("Whether buyers are offered the method. Defaults to true.")),
                ("Position", RecipeStepSchemaBuilders.Integer().Description("The method's place in the zone's list, smallest first.")),
                ("Currency", Nullable(SchemaValueType.String, "The three-letter currency the method's amounts are in. The method only offers rates for orders in that currency.")),
                ("TaxCategoryCode", Nullable(SchemaValueType.String, "The tax category of the shipping charge, or null for the store's default.")),
                ("ClassSurcharges", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(RecipeStepSchemaBuilders.Number()).Description("What each unit of a product in a shipping class (the key) adds to the rate.")),
                ("Properties", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("The provider's settings, keyed by the name of their type.")
                    .Properties(
                        (nameof(FlatRateSettings), RecipeStepSchemaBuilders.Object(
                        [
                            ("Amount", RecipeStepSchemaBuilders.Number().Description("The amount per order.")),
                            ("PerItemAmount", RecipeStepSchemaBuilders.Number().Description("The amount added for each item.")),
                        ]).Description("Settings of the Flat provider.")),
                        (nameof(TableRateSettings), RecipeStepSchemaBuilders.Object(
                        [
                            ("Basis", new JsonSchemaBuilder().AnyOf(RecipeStepSchemaBuilders.String().Enum(Enum.GetNames<TableRateBasis>()), RecipeStepSchemaBuilders.Integer()).Description("What the table looks the order up by: Weight, Subtotal or Quantity.")),
                            ("Rows", RecipeStepSchemaBuilders.Array(RecipeStepSchemaBuilders.Object(
                            [
                                ("From", RecipeStepSchemaBuilders.Number().Description("The value the row starts at.")),
                                ("Amount", RecipeStepSchemaBuilders.Number().Description("The amount charged from that value.")),
                            ])).Description("The rows of the table.")),
                            ("Maximum", Nullable(SchemaValueType.Number, "The most the method charges, or null for no cap.")),
                        ]).Description("Settings of the Table provider.")),
                        (nameof(FreeShippingSettings), RecipeStepSchemaBuilders.Object(
                        [
                            ("MinimumSubtotal", RecipeStepSchemaBuilders.Number().Description("The order subtotal from which shipping is free.")),
                        ]).Description("Settings of the Free provider.")),
                        (nameof(LocalDeliverySettings), RecipeStepSchemaBuilders.Object(
                        [
                            ("PostalCodes", RecipeStepSchemaBuilders.StringArray().Description("The postal codes delivered to; a code ending in * matches every code that starts with it.")),
                            ("Amount", RecipeStepSchemaBuilders.Number().Description("The delivery charge.")),
                            ("FreeFromSubtotal", Nullable(SchemaValueType.Number, "The order subtotal from which delivery is free.")),
                            ("MinimumSubtotal", RecipeStepSchemaBuilders.Number().Description("The order subtotal needed for local delivery.")),
                        ]).Description("Settings of the LocalDelivery provider.")),
                        (nameof(CarrierRateSettings), RecipeStepSchemaBuilders.Object(
                        [
                            ("Carriers", RecipeStepSchemaBuilders.StringArray().Description("The carriers whose quotes are offered. Empty offers every carrier.")),
                            ("MaxRates", RecipeStepSchemaBuilders.Integer().Description("The most quotes offered. Defaults to 3.")),
                            ("MarkupPercent", RecipeStepSchemaBuilders.Number().Description("The percentage added to each quote.")),
                            ("MarkupAmount", RecipeStepSchemaBuilders.Number().Description("The amount added to each quote.")),
                            ("FallbackAmount", Nullable(SchemaValueType.Number, "The amount charged when no carrier quotes, or null to offer nothing.")),
                        ]).Description("Settings of the Carrier provider.")))
                    .AdditionalProperties(true)))
            .AdditionalProperties(true);

        return
        [
            ("Name", RecipeStepSchemaBuilders.String().Description("The zone's unique name, such as Domestic. A zone without a matching ItemId is matched by this name.")),
            ("Position", RecipeStepSchemaBuilders.Integer().Description("The zone's place in the list, smallest first.")),
            ("Countries", RecipeStepSchemaBuilders.StringArray().Description("The two-letter country codes the zone covers. Stored upper case.")),
            ("Regions", RecipeStepSchemaBuilders.StringArray().Description("The region codes the zone covers, such as US-CA (country code, hyphen, region). When the zone lists regions of a country, only those regions of it are covered. Stored upper case.")),
            ("RestOfWorld", RecipeStepSchemaBuilders.Boolean().Description("Whether the zone covers every address no other zone covers.")),
            ("Methods", RecipeStepSchemaBuilders.Array(method).Description("The shipping methods the zone offers. When given, the list replaces the stored one.")),
        ];
    }

    private static JsonSchemaBuilder Nullable(SchemaValueType type, string description)
        => new JsonSchemaBuilder().Type(type | SchemaValueType.Null).Description(description);
}
