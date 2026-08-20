using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.BackgroundTasks;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center Call Recording feature: voice interaction recording orchestration and the
/// recording and monitoring settings screens.
/// </summary>
[Feature(ContactCenterConstants.Feature.Recording)]
public sealed class RecordingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecordingGovernancePolicy, RecordingGovernancePolicy>();
        services.AddScoped<IContactCenterRecordingService, ContactCenterRecordingService>();
        services.AddScoped<IAgentRecordingControlService, AgentRecordingControlService>();
        services.AddScoped<ISecurePauseAutoResumeService, SecurePauseAutoResumeService>();
        services.AddScoped<IRecordingAccessGovernanceService, RecordingAccessGovernanceService>();
        services.AddScoped<IContactCenterEventHandler, RecordingMediaDeletionHandler>();
        services.AddScoped<IRecordingErasureGuard, RecordingErasureGuard>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SecurePauseAutoResumeBackgroundTask>());

        // Recording and monitoring settings screens.
        services.AddSiteDisplayDriver<ContactCenterRecordingSettingsDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var adminOptions = serviceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;
        routes.AddRecordingErasureEndpoint(adminOptions.AdminUrlPrefix);
    }
}

/// <summary>
/// Registers the call-recording workflow tasks, available only when both Orchard Core Workflows and the
/// Recording feature are enabled so the required recording service is always resolvable.
/// </summary>
[Feature(ContactCenterConstants.Feature.Recording)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class ContactCenterRecordingWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<StartCallRecordingTask, StartCallRecordingTaskDisplayDriver>();
        services.AddActivity<StopCallRecordingTask, StopCallRecordingTaskDisplayDriver>();
    }
}

/// <summary>
/// Registers the Orchard Audit Trail receipt for confirmed recording-media deletion.
/// </summary>
[Feature(ContactCenterConstants.Feature.Recording)]
[RequireFeatures("OrchardCore.AuditTrail")]
public sealed class RecordingAuditTrailStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, ContactCenterAuditTrailEventConfiguration>();
        services.AddScoped<IContactCenterEventHandler, RecordingMediaDeletionAuditTrailHandler>();
    }
}
