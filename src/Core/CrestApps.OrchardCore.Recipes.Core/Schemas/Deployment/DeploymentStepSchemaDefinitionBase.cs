using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;

/// <summary>
/// Provides the standard implementation surface for deployment step schema definitions.
/// </summary>
/// <remarks>
/// Derive from this class to describe a deployment step in the <c>deployment</c> recipe step. Implementations
/// only supply the step type name and the members its <c>Step</c> payload accepts; the schema service
/// assembles the surrounding <c>Type</c> and <c>Step</c> envelope.
/// </remarks>
public abstract class DeploymentStepSchemaDefinitionBase : IDeploymentStepSchemaDefinition
{
    /// <inheritdoc />
    public abstract string StepType { get; }

    /// <summary>
    /// Gets the human readable step title. Returns <see langword="null"/> when no title is provided.
    /// </summary>
    protected virtual string DisplayText => null;

    /// <summary>
    /// Gets a description explaining what the deployment step exports.
    /// </summary>
    protected virtual string Description => null;

    /// <summary>
    /// Gets the names of the properties that must be provided in the step's <c>Step</c> payload.
    /// </summary>
    protected virtual IEnumerable<string> RequiredProperties => [];

    /// <summary>
    /// Gets a value indicating whether members beyond the declared ones are accepted in the <c>Step</c>
    /// payload. Defaults to <see langword="true"/> because deployment steps can carry members contributed by
    /// other modules.
    /// </summary>
    protected virtual bool AllowAdditionalProperties => true;

    ValueTask<DeploymentStepSchema> IDeploymentStepSchemaDefinition.GetStepSchemaAsync(
        DeploymentStepSchemaContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BuildStepSchemaAsync(context, cancellationToken);
    }

    /// <summary>
    /// Builds the schema describing the deployment step. Override this method when the schema requires
    /// asynchronous work.
    /// </summary>
    /// <param name="context">The context describing the deployment step being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected virtual ValueTask<DeploymentStepSchema> BuildStepSchemaAsync(
        DeploymentStepSchemaContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildStepSchemaCore(context));

    /// <summary>
    /// Builds the property definitions accepted by the step's <c>Step</c> payload object.
    /// </summary>
    /// <param name="context">The context describing the deployment step being documented.</param>
    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context);

    /// <summary>
    /// Assembles the deployment step schema from the declared metadata and property definitions.
    /// </summary>
    /// <param name="context">The context describing the deployment step being documented.</param>
    protected virtual DeploymentStepSchema BuildStepSchemaCore(DeploymentStepSchemaContext context)
    {
        var properties = GetPropertyDefinitions(context)?.ToArray() ?? [];

        return new DeploymentStepSchema
        {
            DisplayText = DisplayText,
            Description = Description,
            Properties = properties,
            RequiredProperties = RequiredProperties?.ToArray() ?? [],
            AllowAdditionalProperties = AllowAdditionalProperties,
        };
    }
}
