using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "deployment" recipe step — creates or updates deployment plans and their steps.
/// </summary>
public sealed class DeploymentRecipeStep : IRecipeStep
{
    private readonly IDeploymentSchemaService _deploymentSchemaService;

    private JsonSchema _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentRecipeStep"/> class.
    /// </summary>
    /// <param name="deploymentSchemaService">The service that composes the available deployment step schemas.</param>
    public DeploymentRecipeStep(IDeploymentSchemaService deploymentSchemaService)
    {
        _deploymentSchemaService = deploymentSchemaService;
    }

    /// <inheritdoc />
    public string Name => "deployment";

    /// <inheritdoc />
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= await CreateSchemaAsync(cancellationToken);

        return _cached;
    }

    private async ValueTask<JsonSchema> CreateSchemaAsync(CancellationToken cancellationToken)
    {
        var stepSchema = await _deploymentSchemaService.GetStepSchemaAsync(cancellationToken);

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
                                .Items(stepSchema)
                                .Description("Deployment steps that belong to the plan.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("Deployment plans to create or update.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
    }
}
