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
                            .Description("The kind of product the part represents.")))
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
                ("Sku", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The optional stock-keeping unit that uniquely identifies the product.")))
            .AdditionalProperties(true);
}
