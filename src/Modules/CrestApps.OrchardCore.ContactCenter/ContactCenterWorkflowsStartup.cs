using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Orchard Core Workflows bridge: a Contact Center workflow event activity and the
/// handler that triggers it for every published domain event.
/// </summary>
[Feature(ContactCenterConstants.Feature.Workflows)]
public sealed class ContactCenterWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterWorkflowEventTypeProvider, ContactCenterWorkflowEventTypeProvider>();
        services.AddActivity<ContactCenterEvent, ContactCenterEventDisplayDriver>();
        services.AddScoped<IContactCenterEventHandler, ContactCenterWorkflowEventHandler>();
    }
}
