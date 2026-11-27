using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Deployments;

internal sealed class LearningGradeScalesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LearningGradeScalesDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public LearningGradeScalesDeploymentStepDisplayDriver(IStringLocalizer<LearningGradeScalesDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(LearningGradeScalesDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("LearningGradeScalesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LearningGradeScalesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(LearningGradeScalesDeploymentStep step, BuildEditorContext context)
        => View("LearningGradeScalesDeploymentStep_Fields_Edit", step).Location(l => l.Zone("Content", "1").Card(S["Grade Scales"], "10"));
}
