using CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Enqueue Activity workflow task, available only when both Orchard Core Workflows and the
/// Queues feature are enabled so the required queue service is always resolvable.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class ContactCenterQueuesWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<EnqueueActivityTask, EnqueueActivityTaskDisplayDriver>();
    }
}
