using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.MailingLists.Deployments;

/// <summary>
/// Shows the mailing lists step in a deployment plan.
/// </summary>
internal sealed class MailingListsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, MailingListsDeploymentStep>
{
    public override IDisplayResult Display(MailingListsDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("MailingListsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("MailingListsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(MailingListsDeploymentStep step, BuildEditorContext context)
        => View("MailingListsDeploymentStep_Fields_Edit", step).Location("Content");
}
