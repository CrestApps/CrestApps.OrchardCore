using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace CrestApps.OrchardCore.Products.Schemas;

/// <summary>
/// Provides recipe schema support for the <see cref="ProductPart"/> settings and payload.
/// </summary>
public sealed class ProductPartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => nameof(ProductPart);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ProductPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Type", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Undefined", "Good", "Service", "Digital")
                            .Description("The kind of product the part represents.")),
                        ("DefaultCurrency", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Description("The ISO-4217 currency code applied to products of this type when an item does not set its own currency.")),
                        ("SalesMode", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Purchasable", "ListingOnly", "PerItem")
                            .Description("Whether products of this type can be bought online, are only listed, or decide per item.")),
                        ("PriceDisplay", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Show", "OnRequest", "Hidden")
                            .Description("How the price of products of this type is shown to visitors.")),
                        ("OnRequestLabel", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Description("The text shown instead of the price when the price is shown on request.")),
                        ("AllowPriceDisplayOverride", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                            .Description("Whether each product may choose how its own price is shown.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Price", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Number | SchemaValueType.Null)
                    .Description("The product price expressed in major currency units. Empty for a listed product whose price is not shown.")),
                ("Currency", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The ISO-4217 currency code the price is sold in. When empty, the content type's default currency applies.")),
                ("Sku", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The optional stock-keeping unit that uniquely identifies the product.")),
                ("CompareAtPrice", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Number | SchemaValueType.Null)
                    .Description("The optional \"was\" or list price shown struck through when it is higher than the price.")),
                ("IsPurchasable", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether this product can be bought online. Honored only when the content type's sales mode is PerItem.")),
                ("PriceDisplay", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("How this product's price is shown: Show, OnRequest or Hidden. Honored only when the content type allows each product to choose.")))
            .AdditionalProperties(true);
}
