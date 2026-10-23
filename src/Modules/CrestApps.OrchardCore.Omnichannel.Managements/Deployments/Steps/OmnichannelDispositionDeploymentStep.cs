using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel dispositions.
/// </summary>
public sealed class OmnichannelDispositionDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<OmnichannelDispositionDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<OmnichannelDispositionDeploymentStep>("Omnichannel Dispositions");

    /// <inheritdoc />
    public override string Name => OmnichannelDeploymentSteps.Disposition;

    /// <inheritdoc />
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    public override LocalizationSource Title => _title;
}
