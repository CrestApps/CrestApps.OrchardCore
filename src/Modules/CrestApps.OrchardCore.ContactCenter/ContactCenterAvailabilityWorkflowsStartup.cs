using CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Set Agent Presence workflow task, available only when both the Workflows bridge and the
/// Availability feature are enabled so the required presence service is always resolvable.
/// </summary>
[Feature(ContactCenterConstants.Feature.Workflows)]
[RequireFeatures(ContactCenterConstants.Feature.Agents)]
public sealed class ContactCenterAvailabilityWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<SetAgentPresenceTask, SetAgentPresenceTaskDisplayDriver>();
    }
}
