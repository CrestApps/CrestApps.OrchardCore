using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces the JSON schema and metadata that describe a single workflow activity (event or task)
/// inside the <c>WorkflowType</c> recipe step.
/// </summary>
/// <remarks>
/// Implement this interface when a module contributes a custom workflow event or task and wants the
/// generated recipe schema to describe the activity's <c>Properties</c> payload, its category and the
/// outcomes it can produce. Registering the implementation as <see cref="IWorkflowActivitySchemaDefinition"/>
/// is enough for the <c>WorkflowType</c> recipe step to pick it up. Prefer deriving from
/// <see cref="WorkflowActivitySchemaDefinitionBase"/>, which handles caching and the standard schema envelope.
/// </remarks>
public interface IWorkflowActivitySchemaDefinition
{
    /// <summary>
    /// Gets the workflow activity name that this definition describes. This must match
    /// <c>IActivity.Name</c> exactly, for example <c>EmailTask</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the schema and metadata describing the activity.
    /// </summary>
    /// <param name="context">The context describing the activity as registered in the activity library.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<WorkflowActivitySchema> GetActivitySchemaAsync(WorkflowActivitySchemaContext context, CancellationToken cancellationToken = default);
}
