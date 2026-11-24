using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Shows the ticket views deployment step.
/// </summary>
internal sealed class TicketViewsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TicketViewsDeploymentStep>
{
    /// <inheritdoc/>
    public override IDisplayResult Display(TicketViewsDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("TicketViewsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TicketViewsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketViewsDeploymentStep step, BuildEditorContext context)
        => View("TicketViewsDeploymentStep_Fields_Edit", step).Location("Content");
}
