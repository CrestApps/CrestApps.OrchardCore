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

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointDeploymentStep"/> class.
    /// </summary>
    public ContactCenterEntryPointDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.EntryPoint;
        Category = _category;
        Title = _title;
    }
}
