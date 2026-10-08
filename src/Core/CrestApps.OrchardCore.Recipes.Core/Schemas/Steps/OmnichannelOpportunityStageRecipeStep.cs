using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "OmnichannelOpportunityStage" recipe step — imports the opportunity stages of the CRM.
/// </summary>
public sealed class OmnichannelOpportunityStageRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "OmnichannelOpportunityStage";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("OmnichannelOpportunityStage").Description("Recipe step discriminator. Must be 'OmnichannelOpportunityStage'.")),
                ("OpportunityStages", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("opportunity stage")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Unique name of the opportunity stage.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Administrative description of the opportunity stage.")),
                            ("Order", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Position of the stage in the pipeline, lowest first.")),
                            ("Probability", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Default chance, from 0 to 100, that an opportunity in this stage is won.")),
                            ("IsClosed", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an opportunity in this stage is closed.")),
                            ("IsWon", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an opportunity in this stage is won.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("opportunity stage")))
                        .AdditionalProperties(true))
                    .Description("The opportunity stages to create or update.")))
            .Required("name", "OpportunityStages")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
