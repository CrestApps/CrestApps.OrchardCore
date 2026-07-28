using CrestApps.OrchardCore.ContactCenter.Configuration;
using CrestApps.OrchardCore.Core.Configuration;

namespace CrestApps.OrchardCore.ContactCenter.Deployments;

internal sealed class ContactCenterConfigurationDeploymentSource : ConfigurationCatalogDeploymentSourceBase<ContactCenterConfigurationDeploymentStep>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterConfigurationDeploymentSource"/> class.
    /// </summary>
    /// <param name="catalogs">The configuration catalogs registered in the tenant.</param>
    public ContactCenterConfigurationDeploymentSource(IEnumerable<IConfigurationCatalog> catalogs)
        : base(catalogs)
    {
    }

    protected override string Group => ContactCenterConfigurationCatalogs.Group;
}
