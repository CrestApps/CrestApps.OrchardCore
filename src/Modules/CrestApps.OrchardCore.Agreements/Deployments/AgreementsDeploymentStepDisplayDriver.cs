using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Agreements.Deployments;

/// <summary>
/// Shows the agreements step in a deployment plan.
/// </summary>
internal sealed class AgreementsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AgreementsDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public AgreementsDeploymentStepDisplayDriver(IStringLocalizer<AgreementsDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Display(AgreementsDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("AgreementsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("AgreementsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(AgreementsDeploymentStep step, BuildEditorContext context)
        => View("AgreementsDeploymentStep_Fields_Edit", step)
            .Location(l => l.Zone("Content", "1").Card(S["Export"], "10"));
}
