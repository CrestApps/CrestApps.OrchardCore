using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Shows the ticket priorities deployment step.
/// </summary>
internal sealed class TicketPrioritiesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TicketPrioritiesDeploymentStep>
{
    /// <inheritdoc/>
    public override IDisplayResult Display(TicketPrioritiesDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("TicketPrioritiesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TicketPrioritiesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketPrioritiesDeploymentStep step, BuildEditorContext context)
        => View("TicketPrioritiesDeploymentStep_Fields_Edit", step).Location("Content");
}
