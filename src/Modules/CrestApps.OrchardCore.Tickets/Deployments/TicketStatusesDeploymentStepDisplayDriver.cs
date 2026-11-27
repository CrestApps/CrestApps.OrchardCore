using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Shows the ticket statuses deployment step.
/// </summary>
internal sealed class TicketStatusesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TicketStatusesDeploymentStep>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketStatusesDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketStatusesDeploymentStepDisplayDriver(IStringLocalizer<TicketStatusesDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(TicketStatusesDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("TicketStatusesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TicketStatusesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketStatusesDeploymentStep step, BuildEditorContext context)
        => View("TicketStatusesDeploymentStep_Fields_Edit", step).Location(l => l.Zone("Content", "1").Card(S["Export"], "10"));
}
