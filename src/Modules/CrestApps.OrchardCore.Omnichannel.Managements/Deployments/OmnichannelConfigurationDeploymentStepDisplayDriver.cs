using CrestApps.OrchardCore.Core.Configuration;
using CrestApps.OrchardCore.Omnichannel.Managements.Configuration;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments;

internal sealed class OmnichannelConfigurationDeploymentStepDisplayDriver : ConfigurationCatalogDeploymentStepDisplayDriverBase<OmnichannelConfigurationDeploymentStep>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelConfigurationDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="catalogs">The configuration catalogs registered in the tenant.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelConfigurationDeploymentStepDisplayDriver(
        IEnumerable<IConfigurationCatalog> catalogs,
        IStringLocalizer<OmnichannelConfigurationDeploymentStepDisplayDriver> stringLocalizer)
        : base(catalogs, stringLocalizer)
    {
    }

    protected override string Group => OmnichannelConfigurationCatalogs.Group;

    protected override LocalizedString Describe(string stepName)
    {
        return stepName switch
        {
            OmnichannelConfigurationCatalogs.Disposition => S["Dispositions"],
            OmnichannelConfigurationCatalogs.ChannelEndpoint => S["Channel endpoints"],
            OmnichannelConfigurationCatalogs.CampaignGroup => S["Campaign groups"],
            OmnichannelConfigurationCatalogs.Campaign => S["Campaigns"],
            OmnichannelConfigurationCatalogs.SubjectFlowSettings => S["Subject flow settings"],
            _ => base.Describe(stepName),
        };
    }
}
