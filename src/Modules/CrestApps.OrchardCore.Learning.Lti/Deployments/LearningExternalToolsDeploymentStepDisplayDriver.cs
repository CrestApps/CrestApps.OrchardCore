using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Lti.Deployments;

/// <summary>
/// Shows the external tools deployment step in a deployment plan: its title, its summary and its editor.
/// </summary>
internal sealed class LearningExternalToolsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LearningExternalToolsDeploymentStep>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(LearningExternalToolsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("LearningExternalToolsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LearningExternalToolsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc/>
    public override IDisplayResult Edit(LearningExternalToolsDeploymentStep step, BuildEditorContext context)
        => View("LearningExternalToolsDeploymentStep_Fields_Edit", step).Location("Content");
}
