using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Builds JSON schemas describing the workflow activities available on the current tenant.
/// </summary>
public interface IWorkflowActivitySchemaService
{
    /// <summary>
    /// Gets a descriptor for every activity registered in the activity library, merged with any
    /// <see cref="IWorkflowActivitySchemaDefinition"/> contributed for it.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<IReadOnlyList<WorkflowActivityDescriptor>> GetActivityDescriptorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema for a single entry of the <c>Activities</c> array in the <c>WorkflowType</c> recipe step.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetActivitySchemaAsync(CancellationToken cancellationToken = default);
}
