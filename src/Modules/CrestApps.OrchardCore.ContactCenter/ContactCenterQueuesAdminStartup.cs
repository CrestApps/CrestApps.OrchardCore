using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the queue, skill, business-hours, and agent-entitlement administration screens.
/// </summary>
[Feature(ContactCenterConstants.Feature.Admin)]
[RequireFeatures(ContactCenterConstants.Feature.Queues)]
public sealed class ContactCenterQueuesAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDisplayDriver<ActivityQueueGroup, ActivityQueueGroupDisplayDriver>()
            .AddDisplayDriver<ActivityQueue, ActivityQueueDisplayDriver>()
            .AddDisplayDriver<ContactCenterSkill, ContactCenterSkillDisplayDriver>()
            .AddDisplayDriver<BusinessHoursCalendar, BusinessHoursCalendarDisplayDriver>();

        services.AddNavigationProvider<ContactCenterAdminMenu>();
        services.AddNavigationProvider<ContactCenterAgentEntitlementsAdminMenu>();
    }
}
