using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces the JSON schema and metadata that describe a single deployment step inside the
/// <c>Steps</c> array of a deployment plan in the <c>deployment</c> recipe step.
/// </summary>
/// <remarks>
/// Implement this interface when a module contributes a custom deployment step and wants the generated
/// recipe schema to describe the step's <c>Step</c> payload. Registering the implementation as
/// <see cref="IDeploymentStepSchemaDefinition"/> is enough for the <c>deployment</c> recipe step to pick it
/// up. Prefer deriving from
/// <see cref="CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.DeploymentStepSchemaDefinitionBase"/>,
/// which assembles the standard payload envelope.
/// </remarks>
public interface IDeploymentStepSchemaDefinition
{
    /// <summary>
    /// Gets the deployment step type name that this definition describes. This must match the
    /// <c>IDeploymentStepFactory.Name</c> exactly, which is the deployment step's CLR type name, for example
    /// <c>ContentDeploymentStep</c>.
    /// </summary>
    string StepType { get; }

    /// <summary>
    /// Builds the schema and metadata describing the deployment step's <c>Step</c> payload.
    /// </summary>
    /// <param name="context">The context describing the deployment step being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<DeploymentStepSchema> GetStepSchemaAsync(DeploymentStepSchemaContext context, CancellationToken cancellationToken = default);
}
