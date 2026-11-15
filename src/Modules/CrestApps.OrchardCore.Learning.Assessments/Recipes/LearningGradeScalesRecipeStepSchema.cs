using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Assessments.Recipes;

/// <summary>
/// Describes the LearningGradeScales recipe step to CrestApps Recipes.
/// </summary>
internal sealed class LearningGradeScalesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => AssessmentsConstants.GradeScalesRecipeStep;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(AssessmentsConstants.GradeScalesRecipeStep).Description("Recipe step discriminator. Must be 'LearningGradeScales'.")),
                ("GradeScales", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The scale id, a generated unique id. A scale with this id is updated.")),
                        ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the scale.")),
                        ("Bands", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                            .Type(SchemaValueType.Object)
                            .Properties(
                                ("MinPercent", new JsonSchemaBuilder().Type(SchemaValueType.Number).Minimum(0).Maximum(100).Description("The lowest percent of the band.")),
                                ("Label", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The grade shown, such as A or Merit.")),
                                ("Passing", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the band is a pass.")))
                            .Required("MinPercent", "Label")).Description("The bands, any order.")))
                    .Required("Name", "Bands")).Description("The grade scales to create, or update by id.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
