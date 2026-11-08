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
                    .Description("How this product's price is shown: Show, OnRequest or Hidden. Honored only when the content type allows each product to choose.")),
                ("SalePrice", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Number | SchemaValueType.Null)
                    .Description("The price the product sells at during its sale, between SaleStartsUtc and SaleEndsUtc. Outside it, or when it isn't lower than the price, the price applies.")),
                ("SaleStartsUtc", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("When the sale starts, in UTC, or null for a sale that has started.")),
                ("SaleEndsUtc", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("When the sale ends, in UTC, or null for a sale with no end.")),
                ("MinQuantity", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer | SchemaValueType.Null)
                    .Description("The fewest units a cart can hold of the product, or null for one.")),
                ("MaxQuantity", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer | SchemaValueType.Null)
                    .Description("The most units a cart can hold of the product, or null for the store's limit.")),
                ("QuantityStep", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer | SchemaValueType.Null)
                    .Description("The pack size the product is sold in: the quantity has to be a multiple of it.")),
                ("ExternalUrl", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("Where an external product is bought, such as a partner's store. A product with one isn't sold here: its card and page link there instead of offering Add to cart.")),
                ("ExternalButtonText", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("The label of the link to ExternalUrl, or null for \"Buy now\".")),
                ("PriceTiers", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("MinQuantity", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The quantity the discount starts at.")),
                            ("PercentOff", new JsonSchemaBuilder().Type(SchemaValueType.Number).Minimum(0).Maximum(100).Description("The percentage taken off each unit, from 0 to 100.")))
                        .AdditionalProperties(true))
                    .Description("The volume discounts: from a quantity on, each unit costs a percentage less.")),
                ("RoleDiscounts", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Role", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The role's name.")),
                            ("PercentOff", new JsonSchemaBuilder().Type(SchemaValueType.Number).Minimum(0).Maximum(100).Description("The percentage taken off each unit, from 0 to 100.")))
                        .AdditionalProperties(true))
                    .Description("The discounts buyers in a role get, such as a wholesale price for the Wholesale role.")),
                ("AddOns", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("AddOnId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The add-on's stable identifier, which carts and orders keep.")),
                            ("Label", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the buyer sees, such as \"Engraving\".")),
                            ("Kind", new JsonSchemaBuilder().AnyOf(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Text", "Checkbox"), new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Description("Whether the buyer types a text or ticks a box.")),
                            ("Price", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The extra price per unit when the buyer chooses it, or zero. With PerPerson it is per person a unit is for, and with PercentOfPrice it is a percent of the unit's price.")),
                            ("PriceBasis", new JsonSchemaBuilder().AnyOf(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("PerUnit", "PerPerson", "PercentOfPrice"), new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Description("What Price is charged on.")),
                            ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the buyer has to fill it in, such as the name to print on a mug.")))
                        .AdditionalProperties(true))
                    .Description("The personal touches a buyer can add, such as an engraving or gift wrap, each at an extra price.")),
                ("Featured", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether the merchant features this product. Listings can sort featured products first and filter on them.")),
                ("Position", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer | SchemaValueType.Null)
                    .Description("The product's place in the order the merchant arranged: a smaller number comes first in a listing's Recommended sort, and products without one come after those with one.")))
            .AdditionalProperties(true);
}
