using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Shows the ticket statuses deployment step.
/// </summary>
internal sealed class TicketStatusesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TicketStatusesDeploymentStep>
{
    /// <inheritdoc/>
    public override IDisplayResult Display(TicketStatusesDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("TicketStatusesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TicketStatusesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketStatusesDeploymentStep step, BuildEditorContext context)
        => View("TicketStatusesDeploymentStep_Fields_Edit", step).Location("Content");
}
