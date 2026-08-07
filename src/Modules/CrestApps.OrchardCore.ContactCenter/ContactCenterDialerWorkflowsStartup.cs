using CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Schedule Callback workflow task, available only when both the Workflows bridge and the
/// Dialer feature are enabled so the required callback service is always resolvable.
/// </summary>
[Feature(ContactCenterConstants.Feature.Workflows)]
[RequireFeatures(ContactCenterConstants.Feature.Dialer)]
public sealed class ContactCenterDialerWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<ScheduleCallbackTask, ScheduleCallbackTaskDisplayDriver>();
    }
}
