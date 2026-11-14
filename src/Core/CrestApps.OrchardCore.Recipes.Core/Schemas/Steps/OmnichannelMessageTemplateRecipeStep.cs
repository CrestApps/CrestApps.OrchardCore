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
                            ("Body", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("Text inserted into the composer when the template is picked; Liquid placeholders such as {{ Contact.DisplayName }} are filled in.")),
                            ("Scope", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).MaxLength(64).Description("The kind of work the template is for, such as Tickets; empty for the messaging workspace. A scope the site doesn't register is kept.")),
                            ("IsPlainText", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Insert the body exactly as written, without filling in placeholders.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("message template")))
                        .Required("Name", "Body")
                        .AdditionalProperties(true))
                    .Description("The message templates to create or update.")),
            ],
            ["Templates"]);
}
