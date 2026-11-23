using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.MailingLists.Deployments;

/// <summary>
/// Shows the mailing list members step in a deployment plan.
/// </summary>
internal sealed class MailingListMembersDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, MailingListMembersDeploymentStep>
{
    public override IDisplayResult Display(MailingListMembersDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("MailingListMembersDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("MailingListMembersDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(MailingListMembersDeploymentStep step, BuildEditorContext context)
        => View("MailingListMembersDeploymentStep_Fields_Edit", step).Location("Content");
}
