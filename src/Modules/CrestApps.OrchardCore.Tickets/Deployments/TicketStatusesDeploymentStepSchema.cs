using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// The schema of the ticket statuses deployment step.
/// </summary>
internal sealed class TicketStatusesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc/>
    public override string StepType => nameof(TicketStatusesDeploymentStep);

    /// <inheritdoc/>
    protected override string DisplayText => "Ticket statuses";

    /// <inheritdoc/>
    protected override string Description => "Exports every ticket status.";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
