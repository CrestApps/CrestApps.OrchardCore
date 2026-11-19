using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// The schema of the ticket views deployment step.
/// </summary>
internal sealed class TicketViewsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc/>
    public override string StepType => nameof(TicketViewsDeploymentStep);

    /// <inheritdoc/>
    protected override string DisplayText => "Ticket views";

    /// <inheritdoc/>
    protected override string Description => "Exports every ticket view.";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
