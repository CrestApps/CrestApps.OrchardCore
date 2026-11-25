using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Shows the ticket priorities deployment step.
/// </summary>
internal sealed class TicketPrioritiesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TicketPrioritiesDeploymentStep>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketPrioritiesDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketPrioritiesDeploymentStepDisplayDriver(IStringLocalizer<TicketPrioritiesDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(TicketPrioritiesDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("TicketPrioritiesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TicketPrioritiesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketPrioritiesDeploymentStep step, BuildEditorContext context)
        => View("TicketPrioritiesDeploymentStep_Fields_Edit", step).Location(l => l.Zone("Content", "1").Card(S["Export"], "10"));
}
