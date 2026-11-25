using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Shows the ticket views deployment step.
/// </summary>
internal sealed class TicketViewsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, TicketViewsDeploymentStep>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketViewsDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketViewsDeploymentStepDisplayDriver(IStringLocalizer<TicketViewsDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(TicketViewsDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("TicketViewsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("TicketViewsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketViewsDeploymentStep step, BuildEditorContext context)
        => View("TicketViewsDeploymentStep_Fields_Edit", step).Location(l => l.Zone("Content", "1").Card(S["Export"], "10"));
}
