using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports messaging templates.
/// </summary>
public sealed class OmnichannelMessageTemplateDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<OmnichannelMessageTemplateDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<OmnichannelMessageTemplateDeploymentStep>("Messaging Templates");

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelMessageTemplateDeploymentStep"/> class.
    /// </summary>
    public OmnichannelMessageTemplateDeploymentStep()
    {
        Name = MessagingDeploymentSteps.MessageTemplate;
        Category = _category;
        Title = _title;
    }
}
