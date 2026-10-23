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

    /// <inheritdoc />
    public override string Name => MessagingDeploymentSteps.MessageTemplate;

    /// <inheritdoc />
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    public override LocalizationSource Title => _title;
}
