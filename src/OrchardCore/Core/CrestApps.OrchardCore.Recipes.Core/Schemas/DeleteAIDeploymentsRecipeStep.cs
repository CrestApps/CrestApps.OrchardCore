using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "DeleteAIDeployments" recipe step — deletes AI model deployments by name or all.
/// </summary>
public sealed class DeleteAIDeploymentsRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "DeleteAIDeployments";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("DeleteAIDeployments")),
                ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("When true, all deployments will be removed.")),
                ("DeploymentNames", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Specific deployment names to delete. Ignored when IncludeAll is true.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
    }
}
