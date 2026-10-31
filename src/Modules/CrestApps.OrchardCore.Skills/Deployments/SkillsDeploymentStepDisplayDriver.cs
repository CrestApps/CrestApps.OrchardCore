using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Skills.Deployments;

internal sealed class SkillsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, SkillsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(SkillsDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("SkillsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("SkillsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(SkillsDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<SkillsDeploymentStepViewModel>("SkillsDeploymentStep_Fields_Edit", model =>
        {
            model.IncludeAssignments = step.IncludeAssignments;
            model.IncludeCertificationRecords = step.IncludeCertificationRecords;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(SkillsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new SkillsDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        step.IncludeAssignments = model.IncludeAssignments;
        step.IncludeCertificationRecords = model.IncludeCertificationRecords;

        return Edit(step, context);
    }
}
