using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Shows the ticket boards deployment step.
/// </summary>
internal sealed class TicketBoardsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TicketBoardsDeploymentStep>
{
    /// <inheritdoc/>
    public override IDisplayResult Display(TicketBoardsDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("TicketBoardsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TicketBoardsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketBoardsDeploymentStep step, BuildEditorContext context)
        => View("TicketBoardsDeploymentStep_Fields_Edit", step).Location("Content");
}
