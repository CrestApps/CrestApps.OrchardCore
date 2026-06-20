using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "deployment" recipe step — configures deployment plans and targets.
/// </summary>
public sealed class DeploymentRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "deployment";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("deployment").Description("Recipe step discriminator. Must be 'deployment'.")),
                ("Plans", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Deployment plan name.")),
                            ("Steps", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Type", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Deployment step type identifier.")),
                                        ("Step", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true).Description("Raw deployment step payload passed to the step serializer.")))
                                    .Required("Type", "Step")
                                    .AdditionalProperties(true))
                                .Description("Deployment steps that belong to the plan.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("Deployment plans to create or update.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
    }
}
