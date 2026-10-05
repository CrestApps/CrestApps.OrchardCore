using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports messaging templates.
/// </summary>
public sealed class OmnichannelMessageTemplateDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelMessageTemplateDeploymentStep"/> class.
    /// </summary>
    public OmnichannelMessageTemplateDeploymentStep()
    {
        Name = MessagingDeploymentSteps.MessageTemplate;
        Category = LocalizationSource.Create<OmnichannelMessageTemplateDeploymentStep>("Omnichannel");
    }
}
