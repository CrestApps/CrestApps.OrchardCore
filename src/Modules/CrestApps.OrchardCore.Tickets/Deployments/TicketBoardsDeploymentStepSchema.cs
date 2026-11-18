using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// The schema of the ticket boards deployment step.
/// </summary>
internal sealed class TicketBoardsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc/>
    public override string StepType => nameof(TicketBoardsDeploymentStep);

    /// <inheritdoc/>
    protected override string DisplayText => "Ticket boards";

    /// <inheritdoc/>
    protected override string Description => "Exports every ticket board.";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
