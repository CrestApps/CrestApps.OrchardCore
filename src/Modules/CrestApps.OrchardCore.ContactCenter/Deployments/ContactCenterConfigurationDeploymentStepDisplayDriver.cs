using CrestApps.OrchardCore.ContactCenter.Configuration;
using CrestApps.OrchardCore.Core.Configuration;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Deployments;

internal sealed class ContactCenterConfigurationDeploymentStepDisplayDriver : ConfigurationCatalogDeploymentStepDisplayDriverBase<ContactCenterConfigurationDeploymentStep>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterConfigurationDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="catalogs">The configuration catalogs registered in the tenant.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterConfigurationDeploymentStepDisplayDriver(
        IEnumerable<IConfigurationCatalog> catalogs,
        IStringLocalizer<ContactCenterConfigurationDeploymentStepDisplayDriver> stringLocalizer)
        : base(catalogs, stringLocalizer)
    {
    }

    protected override string Group => ContactCenterConfigurationCatalogs.Group;

    protected override LocalizedString Describe(string stepName)
    {
        return stepName switch
        {
            ContactCenterConfigurationCatalogs.Skill => S["Skills"],
            ContactCenterConfigurationCatalogs.QueueGroup => S["Queue groups"],
            ContactCenterConfigurationCatalogs.BusinessHoursCalendar => S["Business-hours calendars"],
            ContactCenterConfigurationCatalogs.Queue => S["Queues"],
            ContactCenterConfigurationCatalogs.EntryPoint => S["Entry points"],
            ContactCenterConfigurationCatalogs.DialerProfile => S["Dialer profiles"],
            ContactCenterConfigurationCatalogs.AgentStateReasonCode => S["Agent state reason codes"],
            _ => base.Describe(stepName),
        };
    }
}
