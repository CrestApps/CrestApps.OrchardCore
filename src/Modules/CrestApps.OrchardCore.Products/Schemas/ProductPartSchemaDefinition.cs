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
                            .Description("The ISO-4217 currency code applied to products of this type when an item does not set its own currency.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Price", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Number)
                    .Description("The product price expressed in major currency units.")),
                ("Currency", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The ISO-4217 currency code the price is sold in. When empty, the content type's default currency applies.")),
                ("Sku", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The optional stock-keeping unit that uniquely identifies the product.")))
            .AdditionalProperties(true);
}
