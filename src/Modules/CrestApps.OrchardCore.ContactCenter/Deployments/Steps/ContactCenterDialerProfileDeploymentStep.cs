using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center dialer profiles.
/// </summary>
public sealed class ContactCenterDialerProfileDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ContactCenterDialerProfileDeploymentStep>("Contact Center");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ContactCenterDialerProfileDeploymentStep>("Contact Center Dialer Profiles");

    /// <inheritdoc />
    public override string Name => ContactCenterDeploymentSteps.DialerProfile;

    /// <inheritdoc />
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    public override LocalizationSource Title => _title;
}
