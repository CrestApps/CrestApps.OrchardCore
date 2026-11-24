using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Shows the desks deployment step.
/// </summary>
internal sealed class TicketDesksDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TicketDesksDeploymentStep>
{
    /// <inheritdoc/>
    public override IDisplayResult Display(TicketDesksDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("TicketDesksDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TicketDesksDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketDesksDeploymentStep step, BuildEditorContext context)
        => View("TicketDesksDeploymentStep_Fields_Edit", step).Location("Content");
}
