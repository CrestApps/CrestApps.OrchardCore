using CrestApps.OrchardCore.BusinessDocuments.Deployments;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.BusinessDocuments.Recipes;

/// <summary>
/// Describes the "BusinessDocumentTemplates" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class BusinessDocumentTemplatesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => BusinessDocumentTemplatesDeploymentStep.StepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(BusinessDocumentTemplatesDeploymentStep.StepName).Description("Recipe step discriminator. Must be 'BusinessDocumentTemplates'.")),
                (BusinessDocumentTemplatesDeploymentStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The template's 26-character id; omit it to create a new one.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the template.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Notes about the template.")),
                            ("Kinds", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The kinds of documents the template is for, such as Invoice or Receipt; empty for every kind.")),
                            ("IsDefault", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the template is used for its kinds when a document names no template.")),
                            ("LogoUrl", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The address of the logo, such as /media/logo.png. The PDF renderer reads only a PNG or JPEG file in the media library.")),
                            ("AccentColor", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Pattern("^#[0-9A-Fa-f]{6}$").Description("The accent color, written #RRGGBB.")),
                            ("PaperSize", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("The paper size: Default, Letter or A4.")),
                            ("Footer", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The text printed at the bottom of the documents.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The document templates to create or update.")))
            .Required("name", BusinessDocumentTemplatesDeploymentStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
