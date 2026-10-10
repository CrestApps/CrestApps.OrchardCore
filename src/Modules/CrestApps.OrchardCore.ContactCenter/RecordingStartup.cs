using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.BackgroundTasks;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
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

        // The notice that the call is recorded: spoken on inbound and automated voice agent calls, read out by an
        // agent on any other call, and recorded as the caller's consent once given.
        services
            .AddScoped<RecordingDisclosureService>()
            .AddScoped<IRecordingDisclosureService>(serviceProvider => serviceProvider.GetRequiredService<RecordingDisclosureService>())
            .AddScoped<IRecordingDisclosureProvider>(serviceProvider => serviceProvider.GetRequiredService<RecordingDisclosureService>());
        // IRecordingAccessGovernanceService is registered by the Recording.Core feature (a dependency of this
        // feature), so voicemail playback can reuse the same governance without enabling full call recording.
        services.AddScoped<IContactCenterEventHandler, RecordingMediaDeletionHandler>();
        services.AddScoped<IRecordingErasureGuard, RecordingErasureGuard>();

        // Recordings start on their own when the tenant records every call, and every recording a provider saves is
        // listed on the call recordings page.
        services.AddScoped<IContactCenterEventHandler, AutomaticCallRecordingHandler>();
        services
            .AddScoped<ICallRecordingStore, CallRecordingStore>()
            .AddScoped<ICallRecordingCatalog, CallRecordingCatalog>()
            .AddScoped<IContactCenterEventHandler, CallRecordingErasureHandler>()
            .AddIndexProvider<CallRecordingIndexProvider>()
            .AddDataMigration<CallRecordingIndexMigrations>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SecurePauseAutoResumeBackgroundTask>());

        // Recording and monitoring settings screens.
        services.AddSiteDisplayDriver<ContactCenterRecordingSettingsDisplayDriver>();

        // The call recordings page: search, playback and the transcript beside it.
        services
            .AddPermissionProvider<CallRecordingPermissionProvider>()
            .AddNavigationProvider<ContactCenterCallRecordingsAdminMenu>()
            .AddResourceConfiguration<ContactCenterCallRecordingsResourceConfiguration>()
            .AddScoped<ICallRecordingTranscriptProvider, AIConversationCallRecordingTranscriptProvider>()
            .AddScoped<CallRecordingAccessEvaluator>()
            .AddScoped<CallRecordingAgentNameResolver>();

        // An activity's page lists the recordings of its calls, for whoever may hear them.
        services
            .AddScoped<ActivityCallRecordingLookup>()
            .AddDisplayDriver<OmnichannelActivity, OmnichannelActivityCallRecordingsDisplayDriver>();
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
