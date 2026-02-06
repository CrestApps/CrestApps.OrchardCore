namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "deployment" recipe step — configures deployment plans and targets.
/// </summary>
public sealed class DeploymentRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "deployment";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("deployment")),
                ("Plans", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("DeploymentSteps", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
                        .Required("Name")
                        .AdditionalProperties(true))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
