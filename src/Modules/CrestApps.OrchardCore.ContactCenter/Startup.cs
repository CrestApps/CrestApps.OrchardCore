using CrestApps.Core.Services;
using CrestApps.OrchardCore.Configuration;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
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
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

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
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ContactCenterStorage.CollectionName));

        services.ValidateTenantOptionsOnActivation();

        services
            .AddOptions<ContactCenterRetentionOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Retention"))
            .Validate(
                options => options.InteractionEventRetentionDays >= 0,
                "'CrestApps:ContactCenter:Retention:InteractionEventRetentionDays' cannot be negative. Use zero to keep interaction events indefinitely.")
            .Validate(
                options => options.ProjectionReplayHorizonDays >= 0,
                "'CrestApps:ContactCenter:Retention:ProjectionReplayHorizonDays' cannot be negative. Use zero to apply no replay floor.")
            .Validate(
                options => options.LegalHoldMinimumDays >= 0,
                "'CrestApps:ContactCenter:Retention:LegalHoldMinimumDays' cannot be negative. Use zero to apply no legal-hold floor.")
            .Validate(
                options => options.InteractionRetentionDays >= 0
                    && options.CallSessionRetentionDays >= 0
                    && options.QueueItemRetentionDays >= 0
                    && options.ActivityReservationRetentionDays >= 0
                    && options.OutboxMessageRetentionDays >= 0
                    && options.WebhookInboxMessageRetentionDays >= 0
                    && options.ProviderCommandRetentionDays >= 0
                    && options.AgentSessionRetentionDays >= 0
                    && options.CallbackRequestRetentionDays >= 0
                    && options.EventMetricRetentionDays >= 0
                    && options.SecureCaptureRetentionDays >= 0
                    && options.ProcessedEventRetentionDays >= 0
                    && options.WorkStateRetentionDays >= 0,
                "Every 'CrestApps:ContactCenter:Retention' window must be zero or greater. Use zero to keep that entity indefinitely.")
            .Validate(
                options => options.ProcessedEventDeliveryEnvelopeDays >= 0,
                "'CrestApps:ContactCenter:Retention:ProcessedEventDeliveryEnvelopeDays' cannot be negative. Use zero to apply no redelivery floor.")
            .Validate(
                options => options.PurgeBatchSize >= 0,
                "'CrestApps:ContactCenter:Retention:PurgeBatchSize' cannot be negative. Use zero to apply the default batch size.")
            .Validate(
                options => options.MaxPurgeBatchesPerCycle >= 0,
                "'CrestApps:ContactCenter:Retention:MaxPurgeBatchesPerCycle' cannot be negative. Use zero to apply the default batch budget.")
            .ValidateOnStart();

        services
            .AddOptions<ContactCenterCoordinationOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Coordination"))
            .Validate(
                options => options.InboundLockTimeout > TimeSpan.Zero,
                "'CrestApps:ContactCenter:Coordination:InboundLockTimeout' must be greater than zero.")
            .Validate(
                options => options.InboundLockExpiration > TimeSpan.Zero,
                "'CrestApps:ContactCenter:Coordination:InboundLockExpiration' must be greater than zero.")
            .Validate(
                options => options.InboundLockExpiration > options.InboundLockTimeout,
                "'CrestApps:ContactCenter:Coordination:InboundLockExpiration' must exceed 'InboundLockTimeout', otherwise the lease expires while a peer is still waiting for it and two nodes route the same call.")
            .ValidateOnStart();

        services
            .AddOptions<ContactCenterTopologyOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Topology"))
            .Validate(
                options => string.IsNullOrWhiteSpace(options.ProfileId)
                    || ContactCenterTopologyProfiles.Find(options.ProfileId) is not null,
                $"'CrestApps:ContactCenter:Topology:ProfileId' is not recognized. Recognized profiles are: {string.Join(", ", ContactCenterTopologyProfiles.All.Select(profile => profile.Id).Order(StringComparer.Ordinal))}.")
            .ValidateOnStart();

        services.AddSingleton<ContactCenterTopologyState>();
        services.AddScoped<IModularTenantEvents, ContactCenterTopologyValidator>();
        services
            .AddOptions<ContactCenterFeatureLifecycleOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:FeatureLifecycle"))
            .Validate(
                options => options.DrainTimeoutSeconds is >= 1 and <= 300,
                "The Contact Center feature drain timeout must be between 1 and 300 seconds.")
            .ValidateOnStart();

        services
            .AddScoped<ContactCenterFeatureLifecycleCoordinator>()
            .AddScoped<IFeatureEventHandler, ContactCenterFeatureLifecycleHandler>()
            .AddSingleton<IContactCenterFeatureWorkManager, ContactCenterFeatureWorkManager>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new ContactCenterFeatureWorkLifecycleParticipant(
                    ContactCenterConstants.Feature.Area,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()))
            .AddScoped<IInteractionStore, InteractionStore>()
            .AddScoped<IInteractionManager, InteractionManager>()
            .AddScoped<IInteractionEventStore, InteractionEventStore>()
            .AddScoped<IInteractionEventUpcastService, DefaultInteractionEventUpcastService>()
            .AddScoped<IContactCenterOutboxStore, ContactCenterOutboxStore>()
            .AddScoped<IContactCenterOutbox, ContactCenterOutbox>()
            .AddScoped<IContactCenterScopeExecutor, ContactCenterScopeExecutor>()
            .AddScoped<IContactCenterWorkStateActivityProjection, ContactCenterWorkStateActivityProjection>()
            .AddScoped<IContactCenterActivityWriter, ContactCenterActivityWriter>()
            .AddScoped<ContactCenterEventDispatchContext>()
            .AddScoped<IContactCenterEventPublisher, DefaultContactCenterEventPublisher>()
            .AddScoped<IContactCenterMetricStore, ContactCenterMetricStore>()
            .AddScoped<IContactCenterMetricDeltaStore, ContactCenterMetricDeltaStore>()
            .AddScoped<IContactCenterMetricRollupService, ContactCenterMetricRollupService>()
            .AddScoped<IContactCenterMetricsService, ContactCenterMetricsService>()
            .AddScoped<IContactCenterProjectionCheckpointStore, ContactCenterProjectionCheckpointStore>()
            .AddScoped<IContactCenterMetricsProjectionMaintenanceService, ContactCenterMetricsProjectionMaintenanceService>()
            .AddScoped<IContactCenterEventDeduplicationService, ContactCenterEventDeduplicationService>()
            .AddScoped<IContactCenterEventHandler, ContactCenterMetricsProjectionHandler>()
            .AddScoped<IContactCenterProcessedEventStore, ContactCenterProcessedEventStore>()
            .AddScoped<IContactCenterRetentionService, ContactCenterRetentionService>()
            .AddScoped<IContactCenterRetentionPolicy, InteractionEventRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, InteractionRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, CallSessionRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterOutboxMessageRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterEventMetricRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterEventMetricDeltaRetentionPolicy>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterProcessedEventRetentionPolicy>()
            .AddScoped<IContactCenterAssistService, ContactCenterAssistService>()
            .AddScoped<ICatalogEntryHandler<Interaction>, InteractionHandler>();

        // The reusable voice media library (hold music, greetings, prompts) referenced by queues, campaigns, and
        // entry points. Registered in the base feature so the library is available wherever those are configured.
        services
            .AddScoped<IVoiceMediaItemStore, VoiceMediaItemStore>()
            .AddScoped<IVoiceMediaItemManager, VoiceMediaItemManager>()
            .AddIndexProvider<VoiceMediaItemIndexProvider>()
            .AddDataMigration<VoiceMediaItemIndexMigrations>();

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
            .AddScoped<ICallSessionStore, CallSessionStore>()
            .AddScoped<ICallSessionManager, CallSessionManager>();

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
            .AddScoped<IContactCenterWorkStateStore, ContactCenterWorkStateStore>()
            .AddScoped<IContactCenterRetentionPolicy, ContactCenterWorkStateRetentionPolicy>()
            .AddScoped<IContactCenterWorkStateManager, ContactCenterWorkStateManager>()
            .AddScoped<IContactCenterWorkStateService, ContactCenterWorkStateService>()
            .AddIndexProvider<ContactCenterWorkStateIndexProvider>()
            .AddDataMigration<ContactCenterWorkStateIndexMigrations>();

        // The call-session index and its migration canonicalize provider identity, and this feature does not
        // depend on Telephony, so the resolver must also be available without the Telephony module.
        services.TryAddSingleton<IProviderIdentityResolver, ProviderIdentityResolver>();

        services
            .AddIndexProvider<CallSessionIndexProvider>()
            .AddDataMigration<CallSessionIndexMigrations>();

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
        services.AddDisplayDriver<DeploymentStep, ContactCenterAgentEntitlementDeploymentStepDisplayDriver>();
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
        services.AddScoped<IModularTenantEvents, SharedHealthCheckEndpointValidator>();
        services.AddContactCenterSharedEndpointHealthCheck();

        services.AddContactCenterHealthChecks();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddContactCenterHealthEndpoints();
    }
}
