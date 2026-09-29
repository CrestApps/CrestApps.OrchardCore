using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "OmnichannelMessageTemplate" recipe step — imports the canned responses an agent can drop into the
/// messaging workspace composer.
/// </summary>
public sealed class OmnichannelMessageTemplateRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "OmnichannelMessageTemplate";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Templates", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("message template")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("Unique template name shown in the composer picker.")),
                            ("Body", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("Text inserted into the composer when the template is picked.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("message template")))
                        .Required("Name", "Body")
                        .AdditionalProperties(true))
                    .Description("The message templates to create or update.")),
            ],
            ["Templates"]);
}
