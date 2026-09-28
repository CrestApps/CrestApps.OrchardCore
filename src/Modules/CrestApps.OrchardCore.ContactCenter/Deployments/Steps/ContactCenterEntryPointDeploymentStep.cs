using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Contact Center entry points.
/// </summary>
public sealed class ContactCenterEntryPointDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointDeploymentStep"/> class.
    /// </summary>
    public ContactCenterEntryPointDeploymentStep()
    {
        Name = ContactCenterDeploymentSteps.EntryPoint;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterEntryPointDeploymentStep(IStringLocalizer<ContactCenterEntryPointDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Contact Center"];
    }
}
