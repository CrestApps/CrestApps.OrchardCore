using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center entry points.
/// </summary>
public sealed class ContactCenterEntryPointDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ContactCenterEntryPointDeploymentStep>("Contact Center");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ContactCenterEntryPointDeploymentStep>("Contact Center Entry Points");

    /// <inheritdoc />
    public override string Name => ContactCenterDeploymentSteps.EntryPoint;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
