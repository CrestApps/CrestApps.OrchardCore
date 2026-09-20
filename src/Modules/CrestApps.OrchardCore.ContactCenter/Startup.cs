using CrestApps.Core.ContactCenter;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Configuration;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.Core.ContactCenter.HealthChecks;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.Core.ContactCenter.Services.Retention;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Drivers;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.Core.Telephony.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.Data;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the services and configuration for the base Contact Center feature.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration used to bind Contact Center options.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterCapability(ContactCenterFeatures.Area, ContactCenterCapabilities.Core);

        services.AddCoreHostSeams();

        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ContactCenterStorage.CollectionName));

        services.ValidateTenantOptionsOnActivation();

        services.AddCoreContactCenter(_shellConfiguration);

        // The unit of work the Contact Center runs its own operations in. It stays with the host because it is
        // a shell scope: the framework method must not hand a standalone host something that only works here.
        services.AddScoped<IContactCenterScopeExecutor, ContactCenterScopeExecutor>();

        services.AddScoped<IModularTenantEvents, ContactCenterTopologyValidator>();

        services
            .AddScoped<ContactCenterFeatureLifecycleCoordinator>()
            .AddScoped<IFeatureEventHandler, ContactCenterFeatureLifecycleHandler>()
            .AddSingleton<IContactCenterFeatureWorkManager, ContactCenterFeatureWorkManager>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new ContactCenterFeatureWorkLifecycleParticipant(
                    ContactCenterCapabilities.Core,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()))
            .AddScoped<IContactCenterEventHandler, ContactCenterMetricsProjectionHandler>()
            .AddScoped<ICatalogEntryHandler<Interaction>, InteractionHandler>();

        services
            .AddScoped<ICatalogEntryHandler<VoiceMediaItem>, VoiceMediaItemHandler>()
            .AddDisplayDriver<VoiceMediaItem, VoiceMediaItemDisplayDriver>()
            .AddIndexProvider<VoiceMediaItemIndexProvider>()
            .AddDataMigration<VoiceMediaItemIndexMigrations>();

        services.AddNavigationProvider<ContactCenterVoiceMediaAdminMenu>();

        // Every Contact Center document written before the models moved records a type name that no longer
        // resolves, so this runs before anything tries to read one.
        services.AddDataMigration<ContactCenterLegacyDocumentTypeNameMigrations>();

        services
            .AddIndexProvider<ContactCenterEventMetricIndexProvider>()
            .AddDataMigration<ContactCenterEventMetricIndexMigrations>()
            .AddIndexProvider<ContactCenterEventMetricDeltaIndexProvider>()
            .AddDataMigration<ContactCenterEventMetricDeltaIndexMigrations>();

        services
            .AddIndexProvider<ContactCenterProcessedEventIndexProvider>()
            .AddDataMigration<ContactCenterProcessedEventIndexMigrations>();

        services
            .AddIndexProvider<ContactCenterProjectionCheckpointIndexProvider>()
            .AddDataMigration<ContactCenterProjectionCheckpointIndexMigrations>();

        services
            .AddIndexProvider<InteractionIndexProvider>()
            .AddDataMigration<InteractionIndexMigrations>();

        services
            .AddIndexProvider<InteractionEventIndexProvider>()
            .AddDataMigration<InteractionEventIndexMigrations>();

        services
            .AddIndexProvider<ContactCenterOutboxMessageIndexProvider>()
            .AddDataMigration<ContactCenterOutboxMessageIndexMigrations>();

        // Routing owns assignment and reservation state in its own document so that a routing transition never
        // contends with a CRM edit of the same activity row. The work state itself is a Contact Center document;
        // the base feature composes Omnichannel Management so its CRM activity projection and administration
        // surfaces are available together.
        services
            .AddIndexProvider<ContactCenterWorkStateIndexProvider>()
            .AddDataMigration<ContactCenterWorkStateIndexMigrations>();

        services
            .AddIndexProvider<CallSessionIndexProvider>()
            .AddDataMigration<CallSessionIndexMigrations>();

        // The host's scheduler for the cycles AddCoreContactCenter registered.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, OutboxDispatchBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ContactCenterRetentionBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ContactCenterMetricRollupBackgroundTask>());
        services.AddPermissionProvider<ContactCenterPermissionProvider>();

        // Contact Center settings screens and administration menu. Each capability owns its administration
        // screens so enabling a capability provides a complete management experience without another toggle.
        services
            .AddResourceConfiguration<ContactCenterExternalTransferResourceConfiguration>()
            .AddSiteDisplayDriver<ContactCenterExternalTransferSettingsDisplayDriver>()
            .AddNavigationProvider<ContactCenterSettingsAdminMenu>();
    }
}

/// <summary>
/// Registers the editors for the Contact Center configuration deployment steps when Orchard Deployment is enabled.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ContactCenterDeploymentAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<DeploymentStep, AgentStateReasonCodeDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterSkillDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterQueueGroupDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterBusinessHoursCalendarDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterQueueDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterEntryPointDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, ContactCenterDialerProfileDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers the Orchard Core Workflows bridge: a Contact Center workflow event activity and the
/// handler that triggers it for every published domain event. Available whenever the base Contact
/// Center feature and Orchard Core Workflows are both enabled, so no separate feature is required.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class ContactCenterWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterWorkflowEventTypeProvider, ContactCenterWorkflowEventTypeProvider>();
        services.AddActivity<ContactCenterEvent, ContactCenterEventDisplayDriver>();
        services.AddScoped<IContactCenterEventHandler, ContactCenterWorkflowEventHandler>();

        // Reaching a customer needs only the activity and its channel's processor, both of which come with the
        // Omnichannel Activities feature this one already depends on. Registering it here rather than under the
        // Dialer keeps an SMS-only tenant from having to enable outbound voice to send a message from a workflow.
        services.AddActivity<StartOmnichannelActivityTask, StartOmnichannelActivityTaskDisplayDriver>();
    }
}

/// <summary>
/// Registers the distributed-dependency health checks that only apply once Redis backs the deployment.
/// </summary>
/// <remarks>
/// The distributed lock, Redis connectivity, and SignalR backplane probes depend on services that only the
/// <c>OrchardCore.Redis</c> feature registers, so they are gated here rather than in the base feature. This
/// mirrors how the Voice feature owns the provider-ingress check: a check must never be registered by a feature
/// whose dependency closure cannot construct it.
/// </remarks>
[RequireFeatures("OrchardCore.Redis", "OrchardCore.HealthChecks")]
public sealed class ContactCenterRedisHealthCheckStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterRedisHealthChecks();
    }
}

/// <summary>
/// Registers the health checks owned by the base Contact Center feature and maps the Contact Center
/// readiness and dependency probes, but only when the <c>OrchardCore.HealthChecks</c> feature is also
/// enabled so a deployment that does not use health checks never pays for them. The health-check options are
/// bound here for the same reason — nothing outside the health checks consumes them — so a deployment without
/// <c>OrchardCore.HealthChecks</c> neither binds nor validates them. The endpoints map here — rather than in
/// the base feature's <c>Configure</c> — because <c>MapHealthChecks</c> resolves the <c>HealthCheckService</c>
/// that only exists once <c>OrchardCore.HealthChecks</c> has registered it; mapping them unconditionally threw
/// at pipeline build time when health checks were not enabled.
/// </summary>
[RequireFeatures("OrchardCore.HealthChecks")]
public sealed class ContactCenterHealthChecksStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterHealthChecksStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration used to bind the health-check options.</param>
    public ContactCenterHealthChecksStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOptions<ContactCenterHealthCheckOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:HealthChecks"))
            .Validate(
                options => options.DeadLetterDegradedThreshold >= 1,
                "'CrestApps:ContactCenter:HealthChecks:DeadLetterDegradedThreshold' must be at least one.")
            .Validate(
                options => options.OverdueBacklogDegradedThreshold >= 1,
                "'CrestApps:ContactCenter:HealthChecks:OverdueBacklogDegradedThreshold' must be at least one.")
            .Validate(
                options => options.ConsecutiveFailuresBeforeUnready >= 1,
                "'CrestApps:ContactCenter:HealthChecks:ConsecutiveFailuresBeforeUnready' must be at least one.")
            .Validate(
                options => options.ConsecutiveSuccessesBeforeReady >= 1,
                "'CrestApps:ContactCenter:HealthChecks:ConsecutiveSuccessesBeforeReady' must be at least one.")
            .Validate(
                options => options.DeadLetterUnhealthyThreshold >= options.DeadLetterDegradedThreshold,
                "'CrestApps:ContactCenter:HealthChecks:DeadLetterUnhealthyThreshold' cannot be lower than 'DeadLetterDegradedThreshold'.")
            .Validate(
                options => options.OverdueBacklogUnhealthyThreshold >= options.OverdueBacklogDegradedThreshold,
                "'CrestApps:ContactCenter:HealthChecks:OverdueBacklogUnhealthyThreshold' cannot be lower than 'OverdueBacklogDegradedThreshold'.")
            .ValidateOnStart();

        services.AddSingleton<SharedHealthEndpointHazardState>();

        // The verdict is the framework's; when it runs is this host's. One tenant-events wrapper drives every
        // registered check, so adding a check is a registration rather than another Orchard lifecycle class.
        services.AddScoped<ISharedHealthEndpointDescriptor, ShellSharedHealthEndpointDescriptor>();
        services.AddScoped<IContactCenterStartupCheck, SharedHealthEndpointStartupCheck>();
        services.AddScoped<IModularTenantEvents, ContactCenterStartupCheckTenantEvents>();
        services.AddContactCenterSharedEndpointHealthCheck();

        services.AddContactCenterHealthChecks();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapContactCenterHealthEndpoints();
    }
}
