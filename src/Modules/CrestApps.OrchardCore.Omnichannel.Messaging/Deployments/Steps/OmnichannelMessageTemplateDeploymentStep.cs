using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelMessageTemplateDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelMessageTemplateDeploymentStep(IStringLocalizer<OmnichannelMessageTemplateDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Omnichannel"];
    }
}
