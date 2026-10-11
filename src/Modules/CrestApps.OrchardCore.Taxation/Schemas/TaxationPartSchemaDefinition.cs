using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Schemas;

/// <summary>
/// Provides recipe schema support for the taxation content part.
/// </summary>
public sealed class TaxationPartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => TaxationConstants.Parts.TaxationPart;

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("TaxationPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("DefaultTaxCategoryCode", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Description("The default tax category code applied to new content items.")),
                        ("DefaultTaxClassificationCode", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Description("The default tax classification code applied to new content items.")),
                        ("AllowClassificationOverride", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                            .Default(true)
                            .Description("Whether the tax classification fields are shown to editors.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Taxable", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether the content item is taxable.")),
                ("TaxCategoryCode", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The tax category code (for example 'Electronics').")),
                ("TaxClassificationCode", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The tax classification code that refines the category.")),
                ("ExternalTaxCode", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("An optional external or provider-specific tax code.")))
            .AdditionalProperties(true);
}
