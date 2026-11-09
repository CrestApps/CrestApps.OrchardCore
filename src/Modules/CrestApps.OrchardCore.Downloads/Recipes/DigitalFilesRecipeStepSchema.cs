using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Downloads.Recipes;

/// <summary>
/// Describes the <c>DigitalFiles</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class DigitalFilesRecipeStepSchema : RecipeStepSchemaBase
{
    public override string Name => DigitalFilesStep.RecipeStepName;

    protected override JsonSchema CreateSchema()
    {
        var file = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("A file to store privately and add to a product's digital files. A product that already has a file of that name (without case sensitivity) keeps it, so running the recipe again adds nothing.")
            .Properties(
                ("ProductId", RecipeStepSchemaBuilders.String().Description("The content item id of the product the file is delivered with. The latest version of the product is updated.")),
                ("FileName", RecipeStepSchemaBuilders.String().Description("The name the file is stored and downloaded under, such as manual.pdf. Unsafe characters are removed.")),
                ("Base64", RecipeStepSchemaBuilders.String().Description("The file's bytes, base64-encoded. Takes precedence over Text.")),
                ("Text", RecipeStepSchemaBuilders.String().Description("The file's content as UTF-8 text, used when Base64 is not given.")))
            .Required("ProductId", "FileName")
            .AnyOf(
                new JsonSchemaBuilder().Required("Base64"),
                new JsonSchemaBuilder().Required("Text"))
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [("Files", RecipeStepSchemaBuilders.Array(file).Description("The files to add. Each must be between 1 byte and 10 MB."))],
            ["Files"]);
    }
}
