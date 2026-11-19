using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// The schema of the desks deployment step.
/// </summary>
internal sealed class TicketDesksDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc/>
    public override string StepType => nameof(TicketDesksDeploymentStep);

    /// <inheritdoc/>
    protected override string DisplayText => "Desks";

    /// <inheritdoc/>
    protected override string Description => "Exports every desk.";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
