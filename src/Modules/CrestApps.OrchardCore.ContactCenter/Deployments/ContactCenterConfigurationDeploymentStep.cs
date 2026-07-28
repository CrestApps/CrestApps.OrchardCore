using CrestApps.OrchardCore.ContactCenter.Configuration;
using CrestApps.OrchardCore.Core.Configuration;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments;

/// <summary>
/// Represents a deployment step that exports Contact Center configuration.
/// </summary>
public sealed class ContactCenterConfigurationDeploymentStep : ConfigurationCatalogDeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterConfigurationDeploymentStep"/> class.
    /// </summary>
    public ContactCenterConfigurationDeploymentStep()
    {
        Name = ContactCenterConfigurationCatalogs.Group;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterConfigurationDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterConfigurationDeploymentStep(IStringLocalizer<ContactCenterConfigurationDeploymentStep> stringLocalizer)
        : this()
    {
        ArgumentNullException.ThrowIfNull(stringLocalizer);

        Category = stringLocalizer["Contact Center"];
    }
}
