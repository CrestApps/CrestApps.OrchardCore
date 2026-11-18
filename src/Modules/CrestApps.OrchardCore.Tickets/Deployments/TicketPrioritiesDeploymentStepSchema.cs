using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// The schema of the ticket priorities deployment step.
/// </summary>
internal sealed class TicketPrioritiesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc/>
    public override string StepType => nameof(TicketPrioritiesDeploymentStep);

    /// <inheritdoc/>
    protected override string DisplayText => "Ticket priorities";

    /// <inheritdoc/>
    protected override string Description => "Exports every ticket priority.";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
