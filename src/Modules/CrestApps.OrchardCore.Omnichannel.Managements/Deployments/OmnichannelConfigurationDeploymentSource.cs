using CrestApps.OrchardCore.Core.Configuration;
using CrestApps.OrchardCore.Omnichannel.Managements.Configuration;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments;

internal sealed class OmnichannelConfigurationDeploymentSource : ConfigurationCatalogDeploymentSourceBase<OmnichannelConfigurationDeploymentStep>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelConfigurationDeploymentSource"/> class.
    /// </summary>
    /// <param name="catalogs">The configuration catalogs registered in the tenant.</param>
    public OmnichannelConfigurationDeploymentSource(IEnumerable<IConfigurationCatalog> catalogs)
        : base(catalogs)
    {
    }

    protected override string Group => OmnichannelConfigurationCatalogs.Group;
}
