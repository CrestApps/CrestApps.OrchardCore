using CrestApps.OrchardCore.Core.Configuration;
using CrestApps.OrchardCore.Omnichannel.Managements.Configuration;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments;

/// <summary>
/// Represents a deployment step that exports Omnichannel configuration.
/// </summary>
public sealed class OmnichannelConfigurationDeploymentStep : ConfigurationCatalogDeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelConfigurationDeploymentStep"/> class.
    /// </summary>
    public OmnichannelConfigurationDeploymentStep()
    {
        Name = OmnichannelConfigurationCatalogs.Group;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelConfigurationDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelConfigurationDeploymentStep(IStringLocalizer<OmnichannelConfigurationDeploymentStep> stringLocalizer)
        : this()
    {
        ArgumentNullException.ThrowIfNull(stringLocalizer);

        Category = stringLocalizer["Omnichannel"];
    }
}
