using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Builds JSON schemas describing the deployment steps available on the current tenant, used by the
/// <c>deployment</c> recipe step to describe each entry of a plan's <c>Steps</c> array.
/// </summary>
public interface IDeploymentSchemaService
{
    /// <summary>
    /// Gets a descriptor for every deployment step available on the tenant, merged with any
    /// <see cref="IDeploymentStepSchemaDefinition"/> contributed for it.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<IReadOnlyList<DeploymentStepDescriptor>> GetStepDescriptorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema for a single entry of a deployment plan's <c>Steps</c> array.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetStepSchemaAsync(CancellationToken cancellationToken = default);
}
