using CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the call-recording workflow tasks, available only when both the Workflows bridge and the
/// Recording feature are enabled so the required recording service is always resolvable.
/// </summary>
[Feature(ContactCenterConstants.Feature.Workflows)]
[RequireFeatures(ContactCenterConstants.Feature.Recording)]
public sealed class ContactCenterRecordingWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<StartCallRecordingTask, StartCallRecordingTaskDisplayDriver>();
        services.AddActivity<StopCallRecordingTask, StopCallRecordingTaskDisplayDriver>();
    }
}
